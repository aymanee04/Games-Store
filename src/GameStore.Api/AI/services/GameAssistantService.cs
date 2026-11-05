using GameStore.Api.Services;
using System.ComponentModel.DataAnnotations;
using GameStore.Api.Exceptions;

namespace GameStore.Api.AI.services;
public class GameAssistantService(
    IOllamaService ollama,
    IGenreService genresService,
    IStudioService studiosService,
    IGameService gamesService) : IGameAssistantService
{
    public async Task<GameGenerationDto> GenerateGameAsync(
        string prompt,
        CancellationToken ct)
    {
        var genres = await genresService.GetAllAsync(ct);
        var studios = await studiosService.GetAllAsync(ct);

        if (genres.Count == 0 || studios.Count == 0)
        {
            throw new InvalidOperationException(
                "You need at least one genre and one studio before generating games.");
        }

        var generated = await ollama.GenerateGameAsync(
            prompt,
            genres.Select(g => g.Name).ToList(),
            studios.Select(s => s.Name).ToList(),
            ct);

        ValidateDraft(generated);

        var genre = genres.FirstOrDefault(g =>
            string.Equals(g.Name, generated.GenreName,
                StringComparison.OrdinalIgnoreCase));

        var studio = studios.FirstOrDefault(s =>
            string.Equals(s.Name, generated.StudioName,
                StringComparison.OrdinalIgnoreCase));

        if (genre is null || studio is null)
        {
            throw new BadRequestException(
                "AI generated a genre or studio that does not exist.");
        }

        return generated with
        {
            GenreName = genre.Name,
            StudioName = studio.Name
        };
    }

    public async Task<GameDto> CreateGameAsync(
        GameGenerationDto draft,
        CancellationToken ct)
    {
        ValidateDraft(draft);

        var genres = await genresService.GetAllAsync(ct);
        var studios = await studiosService.GetAllAsync(ct);

        var genre = genres.FirstOrDefault(g =>
            string.Equals(g.Name, draft.GenreName,
                StringComparison.OrdinalIgnoreCase));

        var studio = studios.FirstOrDefault(s =>
            string.Equals(s.Name, draft.StudioName,
                StringComparison.OrdinalIgnoreCase));

        if (genre is null)
        {
            throw new BadRequestException(
                $"Genre '{draft.GenreName}' does not exist.");
        }

        if (studio is null)
        {
            throw new BadRequestException(
                $"Studio '{draft.StudioName}' does not exist.");
        }

        var request = new CreateGameRequest(
            draft.Name,
            genre.Id,
            studio.Id,
            draft.Price,
            draft.ReleaseDate);

        return await gamesService.CreateAsync(request, ct);
    }

    private static void ValidateDraft(GameGenerationDto draft)
    {
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(
                draft,
                new ValidationContext(draft),
                results,
                validateAllProperties: true))
        {
            throw new BadRequestException(
                string.Join("; ", results.Select(r => r.ErrorMessage)));
        }
    }
}