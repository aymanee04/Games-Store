namespace GameStore.Api.AI.DTOs;

using System.ComponentModel.DataAnnotations;

public record GenerateGameRequest(
    [Required, StringLength(1000, MinimumLength = 5)]
    string Prompt
);
