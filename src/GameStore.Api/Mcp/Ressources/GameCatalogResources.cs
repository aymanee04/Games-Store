using System.ComponentModel;
using System.Text.Json;
using GameStore.Api.Mcp.Setup;
using GameStore.Api.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace GameStore.Api.Mcp.Ressources;

/// <summary>
/// Resources are read-only data identified by a URI. Unlike a tool, the model does not "call"
/// a resource: the host application decides when to load it and shows it to the model as context.
/// </summary>
[McpServerResourceType]
public sealed class GameCatalogResources(
    IGenreService genreService,
    IStudioService studioService,
    IGameService games)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // A fixed URI: listed by resources/list.
    [McpServerResource(UriTemplate = "games://genres", Name = "genres", MimeType = "application/json")]
    [Description("Every genre in the store, with its id and name.")]
    public async Task<string> GetGenresAsync(CancellationToken ct) =>
        JsonSerializer.Serialize(
            (await genreService.GetAllAsync(ct)).Select(g => new { g.Id, g.Name }), Json);

    [McpServerResource(UriTemplate = "games://studios", Name = "studios", MimeType = "application/json")]
    [Description("Every studio in the store, with its id and name.")]
    public async Task<string> GetStudiosAsync(CancellationToken ct) =>
        JsonSerializer.Serialize(
            (await studioService.GetAllAsync(ct)).Select(s => new { s.Id, s.Name }), Json);

    // A URI template: {id} is filled from the URI the client asks for, e.g. games://game/3
    [McpServerResource(UriTemplate = "games://game/{id}", Name = "game", MimeType = "application/json")]
    [Description("One game by id.")]
    public async Task<string> GetGameAsync(string id, CancellationToken ct)
    {
        if (!int.TryParse(id, out var gameId))
            throw new McpException($"'{id}' is not a valid game id.");

        var game = await McpGuard.RunAsync(() => games.GetByIdAsync(gameId, ct));

        return JsonSerializer.Serialize(game, Json);
    }
}