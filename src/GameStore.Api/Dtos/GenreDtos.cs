using System.ComponentModel.DataAnnotations;

namespace GameStore.Api.Dtos;

public record GenreDto(int Id, string Name);

public record CreateGenreRequest(
    [Required, StringLength(50, MinimumLength = 1)] string Name);

public record UpdateGenreRequest(
    [Required, StringLength(50, MinimumLength = 1)] string Name);
