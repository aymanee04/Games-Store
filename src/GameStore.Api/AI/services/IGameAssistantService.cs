namespace GameStore.Api.AI.services;

public interface IGameAssistantService
{
    Task<GameGenerationDto> GenerateGameAsync(
        string prompt,
        CancellationToken ct);

    Task<GameDto> CreateGameAsync(
        GameGenerationDto draft,
        CancellationToken ct);
}