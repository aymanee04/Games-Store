using System.ComponentModel.DataAnnotations;
using GameStore.Api.validations;

namespace GameStore.Api.Dtos;

public record GenreDto(int Id, string Name);

public record CreateGenreRequest(
    [Required, StringLength(50, MinimumLength = 1), NoWhitespace] string Name);

public record UpdateGenreRequest(
    [Required, StringLength(50, MinimumLength = 1), NoWhitespace] string Name);
