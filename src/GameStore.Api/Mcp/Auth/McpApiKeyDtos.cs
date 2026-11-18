using System.ComponentModel.DataAnnotations;

namespace GameStore.Api.Mcp.Auth;

public record CreateMcpKeyRequest(
    [Required, StringLength(100, MinimumLength = 1)]
    string Name,
    [Range(1, 365)]
    int ExpiresInDays = 90,
    bool ReadOnly = false);

/// <summary>Returned once, when the key is created. <see cref="Key"/> cannot be retrieved again.</summary>
public record CreatedMcpKeyResponse(
    Guid Id,
    string Name,
    string Key,
    string KeyPrefix,
    bool ReadOnly,
    DateTime ExpiresAtUtc);

public record McpKeyDto(
    Guid Id,
    string Name,
    string KeyPrefix,
    bool ReadOnly,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? RevokedAtUtc,
    DateTime? LastUsedAtUtc);

/// <summary>Who a valid key stands for.</summary>
public record McpKeyIdentity(
    string UserId,
    string UserName,
    Guid KeyId,
    bool ReadOnly,
    IReadOnlyList<string> Roles);

public static class McpApiKeyDefaults
{
    public const string Scheme = "McpApiKey";
    public const string KeyPrefix = "gsmcp_";

    /// <summary>Role given to read-only keys. It is not an Identity role: it exists only on the key's principal.</summary>
    public const string ReaderRole = "McpReader";

    public const string KeyIdClaim = "mcp_key_id";
    public const string ReadOnlyClaim = "mcp_read_only";
}