namespace GameStore.Api.Entities;

/// <summary>
/// One MCP call (tool call, resource read or prompt request). Deliberately has no foreign key
/// to the user, so the record survives deleting the user or the API key.
/// </summary>
public class McpAuditEntry
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; }

    public string? UserId { get; set; }
    public string? UserName { get; set; }

    /// <summary>Set when the call was authenticated with an API key.</summary>
    public Guid? ApiKeyId { get; set; }

    /// <summary>"jwt" or "api-key".</summary>
    public string AuthMethod { get; set; } = null!;

    /// <summary>"tool", "resource" or "prompt".</summary>
    public string Kind { get; set; } = null!;

    /// <summary>Tool name, resource URI or prompt name.</summary>
    public string Target { get; set; } = null!;

    /// <summary>The arguments as JSON, truncated.</summary>
    public string? ArgumentsJson { get; set; }

    public bool Succeeded { get; set; }
    public string? Error { get; set; }
    public int DurationMs { get; set; }
}