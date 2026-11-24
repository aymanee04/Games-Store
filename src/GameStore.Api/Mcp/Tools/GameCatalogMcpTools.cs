using System.ComponentModel;
using GameStore.Api.AI.Plugins;
using GameStore.Api.Mcp.Setup; // AssistantHelpers (internal, same assembly): name matching + messages
using GameStore.Api.Services;
using GameStore.Shared.Dtos;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace GameStore.Api.Mcp.Tools;

[McpServerToolType]
public sealed class GameCatalogMcpTools(
    IGameService games,
    IGenreService genreService,
    IStudioService studioService)
{
    private const int MaxPageSize = 50;

    [McpServerTool(Name = "search_games", ReadOnly = true)]
    [Description("Searches the GameStore catalog. Every filter is optional and they combine. "
                 + "Returns one page of games with their id, genre, studio, price and release date.")]
    public async Task<PagedResult<GameDto>> SearchGamesAsync(
        [Description("Part of the game title.")] string? name = null,
        [Description("Genre name. It must exist in the store.")] string? genre = null,
        [Description("Studio name. It must exist in the store.")] string? studio = null,
        [Description("Minimum price.")] decimal? minPrice = null,
        [Description("Maximum price.")] decimal? maxPrice = null,
        [Description("Only games released after this year.")] int? releasedAfterYear = null,
        [Description("Only games released before this year.")] int? releasedBeforeYear = null,
        [Description("Page number, starting at 1.")] int page = 1,
        [Description("Games per page, 1 to 50.")] int pageSize = 20,
        CancellationToken ct = default)
    {
        int? genreId = null;
        if (!string.IsNullOrWhiteSpace(genre))
        {
            var all = (await genreService.GetAllAsync(ct)).Select(g => (g.Id, g.Name)).ToList();
            var found = AssistantHelpers.Find(all, genre)
                        ?? throw new McpException(AssistantHelpers.NotFoundMessage("Genre", genre, all));

            genreId = found.Id;
        }

        int? studioId = null;
        if (!string.IsNullOrWhiteSpace(studio))
        {
            var all = (await studioService.GetAllAsync(ct)).Select(s => (s.Id, s.Name)).ToList();
            var found = AssistantHelpers.Find(all, studio)
                        ?? throw new McpException(AssistantHelpers.NotFoundMessage("Studio", studio, all));

            studioId = found.Id;
        }

        // Parameters are built in code (not model-bound), so the range limits are applied here.
        var parameters = new GameQueryParameters
        {
            Search = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            GenreId = genreId,
            StudioId = studioId,
            MinPrice = minPrice is { } min ? Math.Clamp(min, 0, 1000) : null,
            MaxPrice = maxPrice is { } max ? Math.Clamp(max, 0, 1000) : null,
            ReleasedAfterYear = releasedAfterYear is { } a ? Math.Clamp(a, 1, 9999) : null,
            ReleasedBeforeYear = releasedBeforeYear is { } b ? Math.Clamp(b, 1, 9999) : null,
            Page = Math.Max(page, 1),
            PageSize = Math.Clamp(pageSize, 1, MaxPageSize)
        };

        return await McpGuard.RunAsync(() => games.GetAllAsync(parameters, ct));
    }

    [McpServerTool(Name = "get_game", ReadOnly = true)]
    [Description("Gets one game by its id (the id comes from search_games).")]
    public Task<GameDto> GetGameAsync(
        [Description("The game's id.")] int id,
        CancellationToken ct = default) =>
        McpGuard.RunAsync(() => games.GetByIdAsync(id, ct));
}