namespace GameStore.Api.Mcp.Audit;

public interface IMcpAuditService
{
    Task RecordAsync(McpAuditRecord record, CancellationToken ct);

    /// <summary>Newest first. Optionally only one kind: "tool", "resource" or "prompt".</summary>
    Task<McpAuditPage> ListAsync(int page, int pageSize, string? kind, CancellationToken ct);
}