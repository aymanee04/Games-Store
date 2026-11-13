using GameStore.Api.AI.DTOs;
using GameStore.Api.AI.Interfaces;
using GameStore.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameStore.Api.AI.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize(Roles = "Admin")]
public class GameImportController(
    IGameImportService importService, IAdminAssistantService assistant) : ControllerBase
{
    /// <summary>Option 1: extract games from an uploaded CSV file.</summary>
    [HttpPost("extractCsv")]
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

    [HttpPost("assistant/ask")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AssistantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AssistantResponse>> Ask(
        [FromBody] AssistantRequest request,
        CancellationToken ct)
    {
        var response = await assistant.HandleAsync(request.Message, ct);
        return Ok(response);
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