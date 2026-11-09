using System.ComponentModel.DataAnnotations;

namespace GameStore.Api.AI.DTOs;

public record ExtractedGameDto(
    [Required, StringLength(100, MinimumLength = 1)]
    string Name,

    [Required, StringLength(50, MinimumLength = 1)]
    string GenreName,

    [Required, StringLength(100, MinimumLength = 1)]
    string StudioName,

    [Range(0, 1000)]
    decimal Price,

    DateOnly? ReleaseDate
);