using System.Security.Cryptography;
using System.Text;
using GameStore.Api.Data;
using GameStore.Api.Entities;
using GameStore.Api.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace GameStore.Api.Mcp.Auth;

public class McpApiKeyService(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    TimeProvider time,
    ILogger<McpApiKeyService> logger) : IMcpApiKeyService
{
    private const int MaxActiveKeysPerUser = 10;
    private const int PrefixLength = 12;
    private static readonly TimeSpan LastUsedGranularity = TimeSpan.FromMinutes(5);

    public async Task<CreatedMcpKeyResponse> CreateAsync(
        string userId, CreateMcpKeyRequest request, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;

        var active = await db.McpApiKeys.CountAsync(
            k => k.UserId == userId && k.RevokedAtUtc == null && k.ExpiresAtUtc > now, ct);

        if (active >= MaxActiveKeysPerUser)
            throw new BadRequestException(
                $"You already have {MaxActiveKeysPerUser} active API keys. Revoke one first.");

        // 256 random bits: guessing is not feasible, so a plain SHA-256 hash is enough here
        // (no salt or slow hash needed, unlike passwords).
        var rawKey = McpApiKeyDefaults.KeyPrefix + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        var key = new McpApiKey
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            KeyHash = Hash(rawKey),
            KeyPrefix = rawKey[..PrefixLength],
            ReadOnly = request.ReadOnly,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(request.ExpiresInDays)
        };

        db.McpApiKeys.Add(key);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("MCP API key {KeyId} created for user {UserId} (readOnly: {ReadOnly})",
            key.Id, userId, key.ReadOnly);

        return new CreatedMcpKeyResponse(key.Id, key.Name, rawKey, key.KeyPrefix, key.ReadOnly, key.ExpiresAtUtc);
    }

    public Task<List<McpKeyDto>> ListAsync(string userId, CancellationToken ct) =>
        db.McpApiKeys.AsNoTracking()
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAtUtc)
            .Select(k => new McpKeyDto(
                k.Id, k.Name, k.KeyPrefix, k.ReadOnly,
                k.CreatedAtUtc, k.ExpiresAtUtc, k.RevokedAtUtc, k.LastUsedAtUtc))
            .ToListAsync(ct);

    public async Task RevokeAsync(string userId, Guid keyId, CancellationToken ct)
    {
        var key = await db.McpApiKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId, ct)
                  ?? throw new NotFoundException(nameof(McpApiKey), keyId);

        if (key.RevokedAtUtc is null)
        {
            key.RevokedAtUtc = time.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);

            logger.LogInformation("MCP API key {KeyId} revoked by user {UserId}", keyId, userId);
        }
    }

    public async Task<McpKeyIdentity?> ValidateAsync(string rawKey, CancellationToken ct)
    {
        if (!rawKey.StartsWith(McpApiKeyDefaults.KeyPrefix, StringComparison.Ordinal) || rawKey.Length > 200)
            return null;

        var hash = Hash(rawKey);
        var now = time.GetUtcNow().UtcDateTime;

        var key = await db.McpApiKeys.AsNoTracking()
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.KeyHash == hash, ct);

        if (key is null || key.RevokedAtUtc is not null || key.ExpiresAtUtc <= now)
            return null;

        // A key is only as good as its owner: if they stop being an admin, the key stops working.
        if (!await users.IsInRoleAsync(key.User, "Admin"))
            return null;

        // Record "last used", but not on every request (that would be a write per MCP call).
        if (key.LastUsedAtUtc is null || now - key.LastUsedAtUtc > LastUsedGranularity)
        {
            await db.McpApiKeys
                .Where(k => k.Id == key.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(k => k.LastUsedAtUtc, now), ct);
        }

        IReadOnlyList<string> roles = key.ReadOnly
            ? [McpApiKeyDefaults.ReaderRole]
            : (await users.GetRolesAsync(key.User)).ToList();

        return new McpKeyIdentity(
            key.UserId, key.User.UserName ?? key.UserId, key.Id, key.ReadOnly, roles);
    }

    private static string Hash(string rawKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));
}