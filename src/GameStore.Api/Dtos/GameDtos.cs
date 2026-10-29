using System.ComponentModel.DataAnnotations;

namespace GameStore.Api.Dtos;

public record GameDto(int Id, string Name, int GenreId, string Genre, int StudioId, string Studio, decimal Price, DateOnly ReleaseDate);

public record CreateGameRequest(
    [Required, StringLength(100, MinimumLength = 1)]
    string Name,
    [Range(1, int.MaxValue)] int GenreId,
    [Range(1, int.MaxValue)] int StudioId,
    [Range(0.0, 1000.0)] decimal Price,
    DateOnly ReleaseDate);

public record UpdateGameRequest(
    [Required, StringLength(100, MinimumLength = 1)]
    string Name,
    [Range(1, int.MaxValue)] int GenreId,
    [Range(1, int.MaxValue)] int StudioId,
    [Range(0.0, 1000.0)] decimal Price,
    DateOnly ReleaseDate);

/// <summary>Bound from the query string: GET /api/games?genreId=1&amp;search=fifa&amp;page=1&amp;pageSize=10</summary>
public class GameQueryParameters
{
    public int? GenreId { get; init; }

    [StringLength(100)]
    public string? Search { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 10;
}