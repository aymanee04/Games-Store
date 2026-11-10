using System.ComponentModel.DataAnnotations;
using GameStore.Api.validations;
using GameStore.Shared.validations;


public record GameDto(int Id, string Name, int GenreId, string Genre, int StudioId, string Studio, decimal Price, DateOnly ReleaseDate);

public record CreateGameRequest(
    [Required, StringLength(100, MinimumLength = 1), NoWhitespace]
    string Name,
    [Range(1, int.MaxValue)] int GenreId,
    [Range(1, int.MaxValue)] int StudioId,
    [Range(0.0, 1000.0), NoWhitespace] decimal Price,
    [Required, ValidReleaseDate] DateOnly? ReleaseDate
    );

public record UpdateGameRequest(
    [Required, StringLength(100, MinimumLength = 1), NoWhitespace]
    string Name,
    [Range(1, int.MaxValue)] int GenreId,
    [Range(1, int.MaxValue)] int StudioId,
    [Range(0.0, 1000.0), NoWhitespace] decimal Price,
    [Required, ValidReleaseDate] DateOnly ReleaseDate);

/// <summary>Bound from the query string: GET /api/games?genreId=1&amp;search=fifa&amp;page=1&amp;pageSize=10</summary>
public class GameQueryParameters
{
    [Range(1, int.MaxValue)]
    public int? GenreId { get; init; }

    [StringLength(100), NoWhitespace]
    public string? Search { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 10;
}