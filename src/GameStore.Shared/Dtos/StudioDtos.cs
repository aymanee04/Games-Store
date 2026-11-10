using System.ComponentModel.DataAnnotations;
using GameStore.Shared.validations;

namespace GameStore.Shared.Dtos;

public record StudioDtos(int Id, string Name);

public record CreateStudioRequest(
    [Required, StringLength(100, MinimumLength = 1), NoWhitespace]
    string Name);

public record UpdateStudioRequest(
    [Required, StringLength(100, MinimumLength = 1), NoWhitespace]
    string Name);