using System.ComponentModel.DataAnnotations;
using GameStore.Api.validations;

namespace GameStore.Api.Dtos;

public record StudioDtos(int Id, string Name);

public record CreateStudioRequest(
    [Required, StringLength(100, MinimumLength = 1), NoWhitespace]
    string Name);

public record UpdateStudioRequest(
    [Required, StringLength(100, MinimumLength = 1), NoWhitespace]
    string Name);