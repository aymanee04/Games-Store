using System.Security.Claims;
using GameStore.Api.Mcp.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameStore.Api.Mcp.Controllers;

/// <summary>
/// Manage your own MCP API keys. Uses the normal JWT login on purpose: an API key can never
/// create, list or revoke keys, so a leaked key cannot mint more keys.
/// </summary>
[ApiController]
[Route("api/mcp/keys")]
[Authorize(Roles = "Admin")]
public class McpKeysController(IMcpApiKeyService keys) : ControllerBase
{
    private string UserId =>
        User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

    /// <summary>The response contains the key itself. It is shown only this once.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreatedMcpKeyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreatedMcpKeyResponse>> Create(
        [FromBody] CreateMcpKeyRequest request, CancellationToken ct)
    {
        var created = await keys.CreateAsync(UserId, request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<McpKeyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<McpKeyDto>>> List(CancellationToken ct) =>
        Ok(await keys.ListAsync(UserId, ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await keys.RevokeAsync(UserId, id, ct);
        return NoContent();
    }
}