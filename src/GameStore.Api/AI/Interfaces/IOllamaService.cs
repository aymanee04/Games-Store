using GameStore.Api.AI.DTOs;

namespace GameStore.Api.AI.Interfaces;

public interface IOllamaService
{
    /// Creates game proposals from a free-text description written by the user.
    Task<List<ExtractedGameDto>> GenerateGamesAsync(
        string prompt,
        IReadOnlyList<string> genres,
        IReadOnlyList<string> studios,
        CancellationToken ct);
}