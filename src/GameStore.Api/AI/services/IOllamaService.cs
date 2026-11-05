namespace GameStore.Api.AI.services;

public interface IOllamaService
{
    Task<GameGenerationDto> GenerateGameAsync(
        string prompt,
        IReadOnlyList<string> genres,
        IReadOnlyList<string> studios,
        CancellationToken ct);
}