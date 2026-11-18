namespace GameStore.Api.Mcp.Auth;

public interface IMcpApiKeyService
{
    Task<CreatedMcpKeyResponse> CreateAsync(string userId, CreateMcpKeyRequest request, CancellationToken ct);

    Task<List<McpKeyDto>> ListAsync(string userId, CancellationToken ct);

    /// <summary>Revokes one of the user's own keys. Throws NotFoundException if it is not theirs.</summary>
    Task RevokeAsync(string userId, Guid keyId, CancellationToken ct);

    /// <summary>Returns who the key stands for, or null if it is unknown, expired, revoked or its owner is no longer an admin.</summary>
    Task<McpKeyIdentity?> ValidateAsync(string rawKey, CancellationToken ct);
}