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