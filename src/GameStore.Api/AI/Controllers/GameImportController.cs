using GameStore.Api.AI.DTOs;
using GameStore.Api.AI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameStore.Api.AI.Controllers;

[ApiController]
[Route("api/ai/games/import")]
[Authorize(Roles = "Admin")]
public class GameImportController(
    IGameImportService importService) : ControllerBase
{
    /// <summary>Option 1: extract games from an uploaded CSV file.</summary>
    [HttpPost("extract")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(List<ExtractedGameDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ExtractedGameDto>>> Extract(
        [FromForm] ExtractGamesRequest request,
        CancellationToken ct)
    {
        var games = await importService.ExtractGamesAsync(
            request.File,
            ct);

        return Ok(games);
    }

    /// <summary>Option 2: create game proposals from a text prompt.</summary>
    [HttpPost("extract-from-prompt")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(List<ExtractedGameDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<List<ExtractedGameDto>>> ExtractFromPrompt(
        [FromBody] GenerateGameRequest request,
        CancellationToken ct)
    {
        var games = await importService.ExtractGamesFromPromptAsync(
            request.Prompt,
            ct);

        return Ok(games);
    }

    /// <summary>Both options end here: the admin reviews the list, then confirms.</summary>
    [HttpPost("confirm")]
    [ProducesResponseType(typeof(List<GameDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<GameDto>>> Confirm(
        [FromBody] ImportGamesRequest request,
        CancellationToken ct)
    {
        var createdGames = await importService.ImportGamesAsync(
            request,
            ct);

        return Ok(createdGames);
    }
}