using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using GameStore.Api.AI.DTOs;
using GameStore.Api.AI.Interfaces;
using GameStore.Api.Exceptions;
using GameStore.Api.Services;
using GameStore.Shared.Dtos;

namespace GameStore.Api.AI.Services;

public class GameImportService(
    IDocumentExtractionService documentService,
    IGenreService genreService,
    IStudioService studioService,
    IGameService gameService,
    ICsvGameParser csvGameParser,
    ILogger<GameImportService> logger) : IGameImportService
{
    private const int MaxGamesPerImport = 50;

    // ---------- Option 1: CSV upload ----------
    public async Task<List<ExtractedGameDto>> ExtractGamesAsync(
        IFormFile file,
        CancellationToken ct)
    {
        var content = await documentService.ExtractTextAsync(file, ct);

        var games = csvGameParser.Parse(content);

        var genres = await genreService.GetAllAsync(ct);
        var studios = await studioService.GetAllAsync(ct);

        logger.LogInformation(
            "Extracted {Count} games from CSV file '{FileName}'",
            games.Count, file.FileName);
        
        return NormalizeNames(
            games,
            genres.Select(g => g.Name),
            studios.Select(s => s.Name));
    }
    
    // ---------- Shared by both options ----------
    public async Task<List<GameDto>> ImportGamesAsync(
        ImportGamesRequest request,
        CancellationToken ct)
    {
        if (request.Games.Count == 0)
            throw new BadRequestException("No games were provided.");

        if (request.Games.Count > MaxGamesPerImport)
            throw new BadRequestException(
                $"You can import at most {MaxGamesPerImport} games at once.");

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
        
        logger.LogInformation(
            "Imported {Count} games",
            createdGames.Count);

        return createdGames;
    }

    /// <summary>
    /// Snaps genre/studio names to the exact stored spelling (case-insensitive match).
    /// Names that don't exist are left untouched so the admin can see and fix them
    /// before confirming.
    /// </summary>
    private static List<ExtractedGameDto> NormalizeNames(
        IEnumerable<ExtractedGameDto> games,
        IEnumerable<string> genreNames,
        IEnumerable<string> studioNames)
    {
        var genreMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in genreNames) genreMap.TryAdd(name, name);

        var studioMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in studioNames) studioMap.TryAdd(name, name);

        return games.Select(game => game with
        {
            GenreName = genreMap.GetValueOrDefault(game.GenreName, game.GenreName),
            StudioName = studioMap.GetValueOrDefault(game.StudioName, game.StudioName)
        }).ToList();
    }
}