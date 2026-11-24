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

    /// <summary>
    /// Free-text admin request. Reads are answered immediately; creates and updates come
    /// back as a list of proposals. Nothing is saved: the client confirms each proposal by
    /// sending its payload to POST /api/games or PUT /api/games/{id} (see Proposals).
    /// </summary>
    [HttpPost("ask")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AssistantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AssistantResponse>> Ask(
        [FromBody] AssistantRequest request,
        CancellationToken ct)
    {
        // "sub" is what TokenService writes; NameIdentifier covers claim-mapping differences.
        var userId = User.FindFirstValue("sub")
                     ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? "unknown";

        var response = await assistant.HandleAsync(
            request.Message, userId, request.ConversationId, request.Provider, ct);
        return Ok(response);
    }

    /// <summary>Which provider answers by default, and whether this environment lets the page choose.</summary>
    [HttpGet("providers")]
    [ProducesResponseType(typeof(AssistantProviderInfo), StatusCodes.Status200OK)]
    public ActionResult<AssistantProviderInfo> Providers() => Ok(assistant.GetProviderInfo());

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