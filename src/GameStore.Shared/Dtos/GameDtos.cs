using System.ComponentModel.DataAnnotations;
using GameStore.Api.validations;
using GameStore.Shared.validations;

namespace GameStore.Shared.Dtos;

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

public class GameQueryParameters
{
    [Range(1, int.MaxValue)]
    public int? GenreId { get; init; }

    [Range(1, int.MaxValue)]
    public int? StudioId { get; init; }

    [Range(0.0, 1000.0)]
    public decimal? MinPrice { get; init; }

    [Range(0.0, 1000.0)]
    public decimal? MaxPrice { get; init; }

    /// <summary>Only games released after this year (i.e. from January 1st of the next year).</summary>
    [Range(1, 9999)]
    public int? ReleasedAfterYear { get; init; }

    /// <summary>Only games released before this year (i.e. before January 1st of that year).</summary>
    [Range(1, 9999)]
    public int? ReleasedBeforeYear { get; init; }

    [StringLength(100), NoWhitespace]
    public string? Search { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 10;
}