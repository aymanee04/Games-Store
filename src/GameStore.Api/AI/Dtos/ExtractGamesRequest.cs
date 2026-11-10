using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace GameStore.Api.AI.DTOs;

public class ExtractGamesRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;
}