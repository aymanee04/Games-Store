using GameStore.Api.AI.DTOs;

namespace GameStore.Api.AI.Interfaces;

public interface IOllamaService
{
    Task<List<ExtractedGameDto>> ExtractGamesAsync(
        string documentContent,
        IReadOnlyList<string> genres,
        IReadOnlyList<string> studios,
        CancellationToken ct);
}