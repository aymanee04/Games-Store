using System.ComponentModel.DataAnnotations;

namespace GameStore.Api.AI.DTOs;

public record ImportGamesRequest(
    [Required]
    List<ExtractedGameDto> Games
);