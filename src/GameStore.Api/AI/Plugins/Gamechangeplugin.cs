using System.ComponentModel;
using GameStore.Api.AI.DTOs;
using GameStore.Api.AI.Filters;
using GameStore.Api.Services;
using GameStore.Shared.Dtos;
using Microsoft.SemanticKernel;

namespace GameStore.Api.AI.Plugins;

/// <summary>
/// "Write" tools that do NOT write. They validate, then park proposals in the
/// AssistantContext. The admin confirms them in the client through the existing
/// endpoints (POST /api/games, PUT /api/games/{id}). Reads go through IGameService.
/// </summary>
public sealed class GameChangePlugin(IGameService games, AssistantContext context)
{
    private const string NoGuessing = "Ask the admin for it. Never guess or invent values.";
    private const int MaxNameLength = 100;
    private const int MaxBulk = 50;

    // create
    [KernelFunction("propose_create_game")]
    [Description("Prepares adding a NEW game for the admin to review. Nothing is saved. "
                 + "Pass only values the admin actually gave and leave the rest empty. "
                 + "Returns what is missing or wrong, or confirms the proposal is ready.")]
    public async Task<string> ProposeCreateGameAsync(
        [Description("Title of the new game.")] string? name = null,
        [Description("Genre name.")] string? genre = null,
        [Description("Studio name.")] string? studio = null,
        [Description("Price, a number between 0 and 1000.")] decimal? price = null,
        [Description("Release date as YYYY-MM-DD.")] string? releaseDate = null,
        CancellationToken ct = default)
    {
        var problems = new List<string>();
        var missing = new List<string>();

        var title = name?.Trim();
        if (string.IsNullOrWhiteSpace(title)) missing.Add("name");
        else if (title.Length > MaxNameLength) problems.Add($"The name can be at most {MaxNameLength} characters.");

        (int Id, string Name)? genreMatch = null;
        if (string.IsNullOrWhiteSpace(genre)) missing.Add("genre");
        else if ((genreMatch = AssistantHelpers.Find(context.Genres, genre)) is null)
            problems.Add(AssistantHelpers.NotFoundMessage("Genre", genre, context.Genres));

        (int Id, string Name)? studioMatch = null;
        if (string.IsNullOrWhiteSpace(studio)) missing.Add("studio");
        else if ((studioMatch = AssistantHelpers.Find(context.Studios, studio)) is null)
            problems.Add(AssistantHelpers.NotFoundMessage("Studio", studio, context.Studios));

        if (price is null) missing.Add("price");
        else if (price is < 0 or > 1000) problems.Add("The price must be between 0 and 1000.");

        DateOnly? date = null;
        if (string.IsNullOrWhiteSpace(releaseDate)) missing.Add("release date");
        else if (!AssistantHelpers.TryParseDate(releaseDate, out var parsed))
            problems.Add($"'{releaseDate}' is not a valid date. Use YYYY-MM-DD.");
        else if (!AssistantHelpers.IsValidReleaseDate(parsed))
            problems.Add("The release date is out of the allowed range (1950 up to 15 years from now).");
        else date = parsed;

        if (!string.IsNullOrWhiteSpace(title) && title.Length <= MaxNameLength
            && (await FindExactAsync(title, ct)).Count > 0)
            problems.Add($"A game named '{title}' already exists. Propose an update instead.");

        if (missing.Count > 0)
            problems.Add($"Still missing: {string.Join(", ", missing)}.");

        if (problems.Count > 0)
            return string.Join(" ", problems) + NoGuessing;

        var payload = new GameProposalPayload(
            title!, genreMatch!.Value.Id, studioMatch!.Value.Id, price!.Value, date!.Value);

        context.Proposals.Add(new GameProposal(
            "create", "POST", "/api/games", null, payload,
            [
                new("Name", null, title!),
                new("Genre", null, genreMatch.Value.Name),
                new("Studio", null, studioMatch.Value.Name),
                new("Price", null, AssistantHelpers.FormatPrice(payload.Price)),
                new("Release date", null, AssistantHelpers.FormatDate(payload.ReleaseDate))
            ]));

        return $"Proposal ready to add '{title}'. NOTHING is saved yet. "
               + "Tell the admin to review and confirm it.";
    }

    // price change
    [KernelFunction("propose_price_change")]
    [Description("Prepares a price change by a PERCENTAGE for every game that matches the filters "
                 + "(for example -10 lowers prices by 10 percent). This tool does the search and the "
                 + "arithmetic itself, so do not call search_games first. Nothing is saved.")]
    public async Task<string> ProposePriceChangeAsync(
        [Description("Percentage to apply: -10 lowers prices by 10 percent, 15 raises them by 15 percent.")]
        decimal? percentChange = null,
        [Description("Part of the game title.")] string? name = null,
        [Description("Genre name.")] string? genre = null,
        [Description("Studio name.")] string? studio = null,
        [Description("Only games priced at least this.")] decimal? minPrice = null,
        [Description("Only games priced at most this.")] decimal? maxPrice = null,
        [Description("Only games released after this year.")] int? releasedAfterYear = null,
        [Description("Only games released before this year.")] int? releasedBeforeYear = null,
        CancellationToken ct = default)
    {
        if (percentChange is null or 0)
            return "The percentage is missing." + NoGuessing;

        if (percentChange is < -100 or > 500)
            return "The percentage must be between -100 and 500.";

        var filters = new GameFilters(name, genre, studio, minPrice, maxPrice, releasedAfterYear, releasedBeforeYear);

        // Safety: a bulk change must name which games, never "everything".
        if (!filters.HasAny)
            return "Say which games to change (a studio, genre, title, year or price range). "
                   + "I will not change every game at once.";

        var parameters = GameQueryBuilder.Build(context, filters, MaxBulk, out var error);
        if (parameters is null)
            return error!;

        var page = await games.GetAllAsync(parameters, ct);

        if (page.TotalCount == 0)
            return "No games match those filters. Nothing to change.";

        if (page.TotalCount > MaxBulk)
            return $"{page.TotalCount} games match, more than the limit of {MaxBulk}. "
                   + "Ask the admin to narrow the filters.";

        var factor = 1 + percentChange.Value / 100m;
        var lines = new List<string>();
        var skipped = new List<string>();

        foreach (var g in page.Items)
        {
            var newPrice = Math.Round(g.Price * factor, 2, MidpointRounding.AwayFromZero);

            if (newPrice is < 0 or > 1000)
            {
                skipped.Add($"{g.Name} (new price {AssistantHelpers.FormatPrice(newPrice)} is out of range)");
                continue;
            }

            if (newPrice == g.Price)
                continue;

            // PUT replaces the whole game, so the payload carries the unchanged values too.
            context.Proposals.Add(new GameProposal(
                "update", "PUT", $"/api/games/{g.Id}", g.Id,
                new GameProposalPayload(g.Name, g.GenreId, g.StudioId, newPrice, g.ReleaseDate),
                [new ProposedChange("Price", AssistantHelpers.FormatPrice(g.Price), AssistantHelpers.FormatPrice(newPrice))]));

            lines.Add($"{g.Name}: {AssistantHelpers.FormatPrice(g.Price)} -> {AssistantHelpers.FormatPrice(newPrice)}");
        }

        if (lines.Count == 0)
            return "No price would change. Nothing to propose.";

        var summary = $"Prepared {lines.Count} price change(s) of {AssistantHelpers.FormatPrice(percentChange.Value)}%: "
                      + string.Join("; ", lines.Take(10))
                      + (lines.Count > 10 ? $"; and {lines.Count - 10} more" : "")
                      + ". NOTHING is saved yet. Tell the admin to review and confirm.";

        if (skipped.Count > 0)
            summary += $" Skipped: {string.Join("; ", skipped)}.";

        return summary;
    }

    // update one
    [KernelFunction("propose_update_game")]
    [Description("Prepares changing ONE existing game for the admin to review. Nothing is saved. "
                 + "Pass the game's current title and ONLY the values the admin wants to change.")]
    public async Task<string> ProposeUpdateGameAsync(
        [Description("Current title of the existing game.")] string? gameName = null,
        [Description("New title, only if the admin wants to rename it.")] string? newName = null,
        [Description("New genre name.")] string? genre = null,
        [Description("New studio name.")] string? studio = null,
        [Description("New price, a number between 0 and 1000.")] decimal? price = null,
        [Description("New release date as YYYY-MM-DD.")] string? releaseDate = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gameName))
            return "Which game should be changed?" + NoGuessing;

        var wanted = gameName.Trim();

        // One service call gives both the exact match and "did you mean" candidates.
        var candidates = await SearchByNameAsync(wanted, ct);
        var exact = candidates.Where(g => IsSameName(g.Name, wanted)).ToList();

        if (exact.Count == 0)
        {
            var similar = candidates.Select(g => g.Name).Take(5).ToList();

            return similar.Count == 0
                ? $"No game named '{wanted}' exists."
                : $"No game named '{wanted}' exists. Similar: {string.Join(", ", similar)}.";
        }

        if (exact.Count > 1)
            return $"More than one game is named '{wanted}' (ids {string.Join(", ", exact.Select(m => m.Id))}). "
                   + "Ask the admin which one.";

        var current = exact[0];
        var problems = new List<string>();
        var changes = new List<ProposedChange>();

        var finalName = current.Name;
        if (!string.IsNullOrWhiteSpace(newName))
        {
            var candidate = newName.Trim().ToLowerInvariant();   // names are stored lower-cased

            if (candidate.Length > MaxNameLength)
                problems.Add($"The name can be at most {MaxNameLength} characters.");
            else if (candidate != current.Name)
            {
                var clash = (await FindExactAsync(candidate, ct)).Any(g => g.Id != current.Id);

                if (clash)
                    problems.Add($"Another game is already named '{newName.Trim()}'.");
                else
                {
                    finalName = candidate;
                    changes.Add(new ProposedChange("Name", current.Name, candidate));
                }
            }
        }

        var genreId = current.GenreId;
        if (!string.IsNullOrWhiteSpace(genre))
        {
            var found = AssistantHelpers.Find(context.Genres, genre);
            if (found is null)
                problems.Add(AssistantHelpers.NotFoundMessage("Genre", genre, context.Genres));
            else if (found.Value.Id != current.GenreId)
            {
                genreId = found.Value.Id;
                changes.Add(new ProposedChange("Genre", current.Genre, found.Value.Name));
            }
        }

        var studioId = current.StudioId;
        if (!string.IsNullOrWhiteSpace(studio))
        {
            var found = AssistantHelpers.Find(context.Studios, studio);
            if (found is null)
                problems.Add(AssistantHelpers.NotFoundMessage("Studio", studio, context.Studios));
            else if (found.Value.Id != current.StudioId)
            {
                studioId = found.Value.Id;
                changes.Add(new ProposedChange("Studio", current.Studio, found.Value.Name));
            }
        }

        var finalPrice = current.Price;
        if (price is { } newPrice)
        {
            if (newPrice is < 0 or > 1000)
                problems.Add("The price must be between 0 and 1000.");
            else if (newPrice != current.Price)
            {
                finalPrice = newPrice;
                changes.Add(new ProposedChange(
                    "Price", AssistantHelpers.FormatPrice(current.Price), AssistantHelpers.FormatPrice(newPrice)));
            }
        }

        var finalDate = current.ReleaseDate;
        if (!string.IsNullOrWhiteSpace(releaseDate))
        {
            if (!AssistantHelpers.TryParseDate(releaseDate, out var newDate))
                problems.Add($"'{releaseDate}' is not a valid date. Use YYYY-MM-DD.");
            else if (!AssistantHelpers.IsValidReleaseDate(newDate))
                problems.Add("The release date is out of the allowed range (1950 up to 15 years from now).");
            else if (newDate != current.ReleaseDate)
            {
                finalDate = newDate;
                changes.Add(new ProposedChange(
                    "Release date", AssistantHelpers.FormatDate(current.ReleaseDate), AssistantHelpers.FormatDate(newDate)));
            }
        }

        if (problems.Count > 0)
            return string.Join(" ", problems);

        var nothingRequested = string.IsNullOrWhiteSpace(newName)
                               && string.IsNullOrWhiteSpace(genre)
                               && string.IsNullOrWhiteSpace(studio)
                               && price is null
                               && string.IsNullOrWhiteSpace(releaseDate);

        if (nothingRequested)
            return $"No change was given for '{current.Name}'. Ask the admin what to change: "
                   + "name, genre, studio, price or release date.";

        if (changes.Count == 0)
            return $"'{current.Name}' already has those values. Nothing to change.";

        // PUT replaces the whole game, so the payload carries the unchanged values too.
        context.Proposals.Add(new GameProposal(
            "update", "PUT", $"/api/games/{current.Id}", current.Id,
            new GameProposalPayload(finalName, genreId, studioId, finalPrice, finalDate),
            changes));

        return $"Proposal ready to update '{current.Name}'. NOTHING is saved yet. "
               + "Tell the admin to review and confirm it.";
    }
    
    
        // delete
    [KernelFunction("propose_delete_game")]
    [Description("Prepares deleting ONE existing game for the admin to review. Nothing is saved.")]
    public async Task<string> ProposeDeleteGameAsync(
        [Description("Title of the game to delete")] string? gameName = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gameName))
            return "Which game should be changed?" + NoGuessing;

        var wanted = gameName.Trim();

        // One service call gives both the exact match and "did you mean" candidates.
        var candidates = await SearchByNameAsync(wanted, ct);
        var exact = candidates.Where(g => IsSameName(g.Name, wanted)).ToList();

        if (exact.Count == 0)
        {
            var similar = candidates.Select(g => g.Name).Take(5).ToList();

            return similar.Count == 0
                ? $"No game named '{wanted}' exists."
                : $"No game named '{wanted}' exists. Similar: {string.Join(", ", similar)}.";
        }

        if (exact.Count > 1)
            return $"More than one game is named '{wanted}' (ids {string.Join(", ", exact.Select(m => m.Id))}). "
                   + "Ask the admin which one.";

        var current = exact[0];
        
        // DELETE removes the whole game, so the payload is empty.
        context.Proposals.Add(new GameProposal(
            "delete", "DELETE", $"/api/games/{current.Id}", current.Id, null,
        new List<ProposedChange> { new ProposedChange("Status", "Active", "Will be deleted") }));

        return $"Proposal ready to delete '{current.Name}'. NOTHING is saved yet. "
               + "Tell the admin to review and confirm it.";
    }

    // ------------------------------------------------------------------ helpers
    private static bool IsSameName(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    // Games whose name contains the text (the service's Search is a case-insensitive LIKE).
    private async Task<List<GameDto>> SearchByNameAsync(string name, CancellationToken ct)
    {
        var page = await games.GetAllAsync(
            new GameQueryParameters { Search = name.Trim(), Page = 1, PageSize = 100 }, ct);

        return page.Items.ToList();
    }

    private async Task<List<GameDto>> FindExactAsync(string name, CancellationToken ct) =>
        (await SearchByNameAsync(name, ct)).Where(g => IsSameName(g.Name, name)).ToList();
}