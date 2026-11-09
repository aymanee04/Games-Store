namespace GameStore.Api.AI.DTOs;

using System.ComponentModel.DataAnnotations;

public record GenerateGameRequest(
    [Required, StringLength(1000, MinimumLength = 5)]
    string Prompt
);

public record GameGenerationDto(
    [Required, StringLength(100, MinimumLength = 1)]
    string Name,

    [Required, StringLength(50, MinimumLength = 1)]
    string GenreName,

    [Required, StringLength(100, MinimumLength = 1)]
    string StudioName,

    [Range(0.0, 1000.0)]
    decimal Price,

    DateOnly ReleaseDate
);