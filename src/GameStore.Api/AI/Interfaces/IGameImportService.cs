using GameStore.Api.AI.DTOs;

namespace GameStore.Api.AI.Interfaces;

public interface IGameImportService
{
    Task<List<ExtractedGameDto>> ExtractGamesAsync(
        IFormFile file,
        CancellationToken ct);

    Task<List<GameDto>> ImportGamesAsync(
        ImportGamesRequest request,
        CancellationToken ct);
}