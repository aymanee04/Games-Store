using System.ComponentModel.DataAnnotations;
using GameStore.Api.AI.DTOs;
using GameStore.Api.AI.Interfaces;
using GameStore.Api.Exceptions;
using GameStore.Api.Services;

namespace GameStore.Api.AI.Services;

public class GameImportService(
    IDocumentExtractionService documentService,
    IOllamaService ollamaService,
    IGenreService genreService,
    IStudioService studioService,
    IGameService gameService,
    ICsvGameParser csvGameParser) : IGameImportService
{
    public async Task<List<ExtractedGameDto>> ExtractGamesAsync(
        IFormFile file,
        CancellationToken ct)
    {
        var content = await documentService.ExtractTextAsync(file, ct);

        var games = csvGameParser.Parse(content);

        var genres = await genreService.GetAllAsync(ct);
        var studios = await studioService.GetAllAsync(ct);

        var validGenres = genres.Select(g => g.Name).ToHashSet(
            StringComparer.OrdinalIgnoreCase);

        var validStudios = studios.Select(s => s.Name).ToHashSet(
            StringComparer.OrdinalIgnoreCase);

        return games.Select(game => game with
        {
            GenreName = validGenres.Contains(game.GenreName)
                ? genres.First(g => string.Equals(
                    g.Name, game.GenreName, StringComparison.OrdinalIgnoreCase)).Name
                : game.GenreName,

            StudioName = validStudios.Contains(game.StudioName)
                ? studios.First(s => string.Equals(
                    s.Name, game.StudioName, StringComparison.OrdinalIgnoreCase)).Name
                : game.StudioName
        }).ToList();
    }

    public async Task<List<GameDto>> ImportGamesAsync(
        ImportGamesRequest request,
        CancellationToken ct)
    {
        if (request.Games.Count == 0)
            throw new BadRequestException("No games were provided.");

        if (request.Games.Count > 50)
            throw new BadRequestException("You can import at most 50 games at once.");

        var genres = await genreService.GetAllAsync(ct);
        var studios = await studioService.GetAllAsync(ct);

        // Validate every game before starting the import.
        var validatedGames = new List<CreateGameRequest>();

        foreach (var game in request.Games)
        {
            var validationResults = new List<ValidationResult>();

            if (!Validator.TryValidateObject(
                    game,
                    new ValidationContext(game),
                    validationResults,
                    validateAllProperties: true))
            {
                throw new BadRequestException(
                    string.Join("; ",
                        validationResults.Select(r => r.ErrorMessage)));
            }

            var genre = genres.FirstOrDefault(g =>
                string.Equals(
                    g.Name,
                    game.GenreName,
                    StringComparison.OrdinalIgnoreCase));

            var studio = studios.FirstOrDefault(s =>
                string.Equals(
                    s.Name,
                    game.StudioName,
                    StringComparison.OrdinalIgnoreCase));

            if (genre is null)
                throw new BadRequestException(
                    $"Genre '{game.GenreName}' does not exist.");

            if (studio is null)
                throw new BadRequestException(
                    $"Studio '{game.StudioName}' does not exist.");

            validatedGames.Add(new CreateGameRequest(
                game.Name,
                genre.Id,
                studio.Id,
                game.Price,
                game.ReleaseDate
            ));
        }

        // Reuse the existing game creation logic.
        var createdGames = new List<GameDto>();

        foreach (var gameRequest in validatedGames)
        {
            var created = await gameService.CreateAsync(gameRequest, ct);
            createdGames.Add(created);
        }

        return createdGames;
    }
}