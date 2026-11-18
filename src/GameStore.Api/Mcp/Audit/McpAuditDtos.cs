namespace GameStore.Api.Mcp.Audit;

/// <summary>What the audit filter hands to the audit service for one MCP call.</summary>
public record McpAuditRecord(
    string? UserId,
    string? UserName,
    Guid? ApiKeyId,
    string Kind,
    string Target,
    string? ArgumentsJson,
    bool Succeeded,
    string? Error,
    int DurationMs);

public record McpAuditDto(
    long Id,
    DateTime TimestampUtc,
    string? UserId,
    string? UserName,
    Guid? ApiKeyId,
    string AuthMethod,
    string Kind,
    string Target,
    string? ArgumentsJson,
    bool Succeeded,
    string? Error,
    int DurationMs);

public record McpAuditPage(List<McpAuditDto> Items, int TotalCount, int Page, int PageSize);