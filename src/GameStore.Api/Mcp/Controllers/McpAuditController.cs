using GameStore.Api.Mcp.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameStore.Api.Mcp.Controllers;

[ApiController]
[Route("api/mcp/audit")]
[Authorize(Roles = "Admin")]
public class McpAuditController(IMcpAuditService audit) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(McpAuditPage), StatusCodes.Status200OK)]
    public async Task<ActionResult<McpAuditPage>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? kind = null,
        CancellationToken ct = default) =>
        Ok(await audit.ListAsync(page, pageSize, kind, ct));
}