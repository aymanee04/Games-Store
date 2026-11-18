using System.ComponentModel;
using GameStore.Api.AI.Plugins;
using GameStore.Api.Mcp.Setup; // AssistantHelpers (internal, same assembly)
using GameStore.Api.Services;
using GameStore.Shared.Dtos;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace GameStore.Api.Mcp.Tools;
/// <summary>
/// Create, update and delete. Unlike the assistant, these WRITE when called: an MCP client
/// normally asks its user to approve a tool call, and the [McpServerTool] hints below
/// (Destructive, Idempotent...) tell the client how careful to be. The server still protects
/// itself: the endpoint is Admin-only, every value is validated here, and delete needs the
/// game's name as a second check.
///
/// Validation lives here because the DataAnnotations on CreateGameRequest/UpdateGameRequest
/// are only applied by MVC model binding. Calling the service directly skips them.
/// </summary>
[McpServerToolType]
public sealed class GameManagementMcpTools(
    IGameService games,
    IGenreService genreService,
    IStudioService studioService)
{
    private const int MaxNameLength = 100;

    // ------------------------------------------------------------------ create
    [McpServerTool(Name = "create_game", Destructive = false, Idempotent = false)]
    [Description("Adds a NEW game to the store. The genre and studio must already exist. "
                 + "Fails if a game with the same name already exists.")]
    public Task<GameDto> CreateGameAsync(
        [Description("Title of the game, 1 to 100 characters.")] string name,
        [Description("Genre name. It must exist in the store.")] string genre,
        [Description("Studio name. It must exist in the store.")] string studio,
        [Description("Price from 0 to 1000.")] decimal price,
        [Description("Release date as YYYY-MM-DD.")] string releaseDate,
        CancellationToken ct = default) =>
        McpGuard.RunAsync(async () =>
        {
            var problems = new List<string>();

            var title = (name ?? "").Trim();
            if (title.Length is 0 or > MaxNameLength)
                problems.Add($"The name must be 1 to {MaxNameLength} characters.");

            if (price is < 0 or > 1000)
                problems.Add("The price must be between 0 and 1000.");

            DateOnly date = default;
            if (!AssistantHelpers.TryParseDate(releaseDate ?? "", out date))
                problems.Add($"'{releaseDate}' is not a valid date. Use YYYY-MM-DD.");
            else if (!AssistantHelpers.IsValidReleaseDate(date))
                problems.Add("The release date is out of the allowed range (1950 up to 15 years from now).");

            var genres = (await genreService.GetAllAsync(ct)).Select(g => (g.Id, g.Name)).ToList();
            var studios = (await studioService.GetAllAsync(ct)).Select(s => (s.Id, s.Name)).ToList();

            var genreMatch = AssistantHelpers.Find(genres, genre ?? "");
            if (genreMatch is null)
                problems.Add(AssistantHelpers.NotFoundMessage("Genre", genre ?? "", genres));

            var studioMatch = AssistantHelpers.Find(studios, studio ?? "");
            if (studioMatch is null)
                problems.Add(AssistantHelpers.NotFoundMessage("Studio", studio ?? "", studios));

            if (title.Length is > 0 and <= MaxNameLength && await NameExistsAsync(title, exceptId: null, ct))
                problems.Add($"A game named '{title}' already exists. Use update_game to change it.");

            if (problems.Count > 0)
                throw new McpException(string.Join(" ", problems));

            return await games.CreateAsync(
                new CreateGameRequest(title, genreMatch!.Value.Id, studioMatch!.Value.Id, price, date), ct);
        });

    // ------------------------------------------------------------------ update
    [McpServerTool(Name = "update_game", Idempotent = true)]
    [Description("Changes an EXISTING game, found by its id (from search_games). "
                 + "Pass only the values to change; everything else stays as it is.")]
    public Task<GameDto> UpdateGameAsync(
        [Description("The game's id.")] int id,
        [Description("New title, 1 to 100 characters.")] string? name = null,
        [Description("New genre name. It must exist in the store.")] string? genre = null,
        [Description("New studio name. It must exist in the store.")] string? studio = null,
        [Description("New price from 0 to 1000.")] decimal? price = null,
        [Description("New release date as YYYY-MM-DD.")] string? releaseDate = null,
        CancellationToken ct = default) =>
        McpGuard.RunAsync(async () =>
        {
            var current = await games.GetByIdAsync(id, ct);   // NotFoundException -> McpException

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(genre)
                && string.IsNullOrWhiteSpace(studio) && price is null && string.IsNullOrWhiteSpace(releaseDate))
                throw new McpException("Nothing to change. Give at least one of: name, genre, studio, price, releaseDate.");

            var problems = new List<string>();

            var finalName = current.Name;
            if (!string.IsNullOrWhiteSpace(name))
            {
                var title = name.Trim();
                if (title.Length > MaxNameLength)
                    problems.Add($"The name must be 1 to {MaxNameLength} characters.");
                else if (await NameExistsAsync(title, exceptId: id, ct))
                    problems.Add($"Another game is already named '{title}'.");
                else
                    finalName = title;
            }

            var finalGenreId = current.GenreId;
            if (!string.IsNullOrWhiteSpace(genre))
            {
                var genres = (await genreService.GetAllAsync(ct)).Select(g => (g.Id, g.Name)).ToList();
                var found = AssistantHelpers.Find(genres, genre);
                if (found is null)
                    problems.Add(AssistantHelpers.NotFoundMessage("Genre", genre, genres));
                else
                    finalGenreId = found.Value.Id;
            }

            var finalStudioId = current.StudioId;
            if (!string.IsNullOrWhiteSpace(studio))
            {
                var studios = (await studioService.GetAllAsync(ct)).Select(s => (s.Id, s.Name)).ToList();
                var found = AssistantHelpers.Find(studios, studio);
                if (found is null)
                    problems.Add(AssistantHelpers.NotFoundMessage("Studio", studio, studios));
                else
                    finalStudioId = found.Value.Id;
            }

            var finalPrice = current.Price;
            if (price is { } newPrice)
            {
                if (newPrice is < 0 or > 1000)
                    problems.Add("The price must be between 0 and 1000.");
                else
                    finalPrice = newPrice;
            }

            var finalDate = current.ReleaseDate;
            if (!string.IsNullOrWhiteSpace(releaseDate))
            {
                if (!AssistantHelpers.TryParseDate(releaseDate, out var newDate))
                    problems.Add($"'{releaseDate}' is not a valid date. Use YYYY-MM-DD.");
                else if (!AssistantHelpers.IsValidReleaseDate(newDate))
                    problems.Add("The release date is out of the allowed range (1950 up to 15 years from now).");
                else
                    finalDate = newDate;
            }

            if (problems.Count > 0)
                throw new McpException(string.Join(" ", problems));

            // The service replaces the whole game, so the unchanged values are sent too.
            await games.UpdateAsync(
                id, new UpdateGameRequest(finalName, finalGenreId, finalStudioId, finalPrice, finalDate), ct);

            return await games.GetByIdAsync(id, ct);
        });

    // ------------------------------------------------------------------ delete
    [McpServerTool(Name = "delete_game", Destructive = true, Idempotent = true)]
    [Description("Permanently deletes ONE game. This cannot be undone. "
                 + "Both the id and the game's exact name are required, as a safety check.")]
    public Task<string> DeleteGameAsync(
        [Description("The game's id (from search_games).")] int id,
        [Description("The game's exact current name. It must match the game with this id.")] string confirmName,
        CancellationToken ct = default) =>
        McpGuard.RunAsync(async () =>
        {
            var current = await games.GetByIdAsync(id, ct);

            if (!string.Equals(current.Name, (confirmName ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                throw new McpException(
                    $"confirmName does not match. Game {id} is named '{current.Name}'. Nothing was deleted.");

            await games.DeleteAsync(id, ct);

            return $"Deleted game {id} ('{current.Name}').";
        });

    // ------------------------------------------------------------------ helpers
    // The service has no "name exists" check, so look through its search (a LIKE match) and compare exactly.
    private async Task<bool> NameExistsAsync(string name, int? exceptId, CancellationToken ct)
    {
        var page = await games.GetAllAsync(
            new GameQueryParameters { Search = name.Trim(), Page = 1, PageSize = 100 }, ct);

        return page.Items.Any(g =>
            g.Id != exceptId && string.Equals(g.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}