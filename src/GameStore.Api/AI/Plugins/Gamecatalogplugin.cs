using System.ComponentModel;
using System.Text;
using GameStore.Api.AI.Filters;
using GameStore.Api.Services;
using Microsoft.SemanticKernel;

namespace GameStore.Api.AI.Plugins;

/// <summary>Read-only tools. Safe to run immediately. Goes through IGameService.</summary>
public sealed class GameCatalogPlugin(IGameService games, AssistantContext context)
{
    private const int MaxRows = 20;

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
        CancellationToken ct = default)
    {
        var parameters = GameQueryBuilder.Build(
            context,
            new GameFilters(name, genre, studio, minPrice, maxPrice, releasedAfterYear, releasedBeforeYear),
            MaxRows,
            out var error);

        if (parameters is null)
            return error!;

        var page = await games.GetAllAsync(parameters, ct);

        // Structured list for the client; compact text for the model.
        context.Games = page.Items.ToList();

        if (page.TotalCount == 0)
            return "No games match.";
        

        var sb = new StringBuilder();
        sb.AppendLine(page.TotalCount > MaxRows
            ? $"{page.TotalCount} games match, showing the first {MaxRows}:"
            : $"{page.TotalCount} game(s) match:");

        foreach (var g in page.Items)
            sb.AppendLine($"- {g.Name} | {g.Genre} | {g.Studio} | "
                          + $"{AssistantHelpers.FormatPrice(g.Price)} | {AssistantHelpers.FormatDate(g.ReleaseDate)}");

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