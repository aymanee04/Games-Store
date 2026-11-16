using System.Security.Claims;
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

    [HttpPost("ask")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AssistantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AssistantResponse>> Ask(
        [FromBody] AssistantRequest request,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue("sub")
                     ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? "unknown";
 
        var response = await assistant.HandleAsync(request.Message, userId, request.ConversationId, ct);
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