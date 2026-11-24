namespace GameStore.Api.Entities;

/// <summary>
/// A long-lived credential for the /mcp endpoint, owned by one admin. The key itself is never
/// stored: only its SHA-256 hash, plus a short prefix so the owner can tell keys apart.
/// </summary>
public class McpApiKey
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;

    public string Name { get; set; } = null!;

    /// <summary>SHA-256 of the key, as 64 hex characters.</summary>
    public string KeyHash { get; set; } = null!;

    public string KeyPrefix { get; set; } = null!;

    /// <summary>A read-only key can only call read tools; it never gets the Admin role.</summary>
    public bool ReadOnly { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
}