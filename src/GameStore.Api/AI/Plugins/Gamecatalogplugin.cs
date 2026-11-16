using System.ComponentModel;
using System.Text;
using GameStore.Api.AI.Filters;
using GameStore.Api.Services;
using Microsoft.SemanticKernel;

namespace GameStore.Api.AI.Plugins;

/// <summary>Read-only tools. Safe to run immediately. Goes through IGameService.</summary>
public sealed class GameCatalogPlugin(IGameService games, AssistantContext context)
{
    private const int MaxRows = 50;   // games per page (also the most the client table shows per page)

    [KernelFunction("search_games")]
    [Description("Searches the store catalog. Every filter is optional. Use it for any question "
                 + "about which games exist, their price, genre, studio or release date.")]
    public async Task<string> SearchGamesAsync(
        [Description("Part of the game title.")] string? name = null,
        [Description("Genre name.")] string? genre = null,
        [Description("Studio name.")] string? studio = null,
        [Description("Minimum price.")] decimal? minPrice = null,
        [Description("Maximum price.")] decimal? maxPrice = null,
        [Description("Only games released after this year.")] int? releasedAfterYear = null,
        [Description("Only games released before this year.")] int? releasedBeforeYear = null,
        [Description("Page number, starting at 1. When the result says more pages remain, call again "
                     + "with the same filters and the next page number.")] int pageNumber = 1,
        CancellationToken ct = default)
    {
        var parameters = GameQueryBuilder.Build(
            context,
            new GameFilters(name, genre, studio, minPrice, maxPrice, releasedAfterYear, releasedBeforeYear),
            MaxRows,
            out var error,
            pageNumber);

        if (parameters is null)
            return error!;

        var page = await games.GetAllAsync(parameters, ct);

        if (page.TotalCount == 0)
            return "No games match.";

        var totalPages = (int)Math.Ceiling(page.TotalCount / (double)MaxRows);

        if (page.Items.Count == 0)
            return $"There is no page {pageNumber}. {page.TotalCount} games match and the last page is {totalPages}.";
        
        if (pageNumber <= 1 || context.Games is null)
            context.Games = page.Items.ToList();
        else
            context.Games.AddRange(page.Items);

        var sb = new StringBuilder();

        sb.AppendLine(totalPages <= 1
            ? $"{page.TotalCount} game(s) match:"
            : $"{page.TotalCount} games match. Page {page.Page} of {totalPages}:");

        foreach (var g in page.Items)
            sb.AppendLine($"- {g.Name} | {g.Genre} | {g.Studio} | "
                          + $"{AssistantHelpers.FormatPrice(g.Price)} | {AssistantHelpers.FormatDate(g.ReleaseDate)}");

        if (page.Page < totalPages)
            sb.AppendLine($"More games remain ({page.TotalCount - page.Page * MaxRows} not shown yet). "
                          + $"Call search_games again with the same filters and pageNumber={page.Page + 1}. "
                          + "If the question needs all the games, read every page before answering.");
        else if (totalPages > 1)
            sb.AppendLine($"That was the last page: you have now seen all {page.TotalCount} games.");

        sb.AppendLine("The admin already sees these games in a table. Do not list them again in your reply.");

        return sb.ToString();
    }

    [KernelFunction("list_genres_and_studios")]
    [Description("Lists every genre and studio that exists in the store.")]
    public string ListGenresAndStudios()
    {
        return
            $"Genres: {(context.Genres.Count == 0 ? "none" : string.Join(", ", context.Genres.Select(g => g.Name)))}\n"
            + $"Studios: {(context.Studios.Count == 0 ? "none" : string.Join(", ", context.Studios.Select(s => s.Name)))}";
    }
}