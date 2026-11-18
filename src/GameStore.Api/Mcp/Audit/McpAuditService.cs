using GameStore.Api.Data;
using GameStore.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameStore.Api.Mcp.Audit;

public class McpAuditService(
    AppDbContext db,
    IServiceScopeFactory scopes,
    TimeProvider time) : IMcpAuditService
{
    private const int MaxArgumentChars = 4000;
    private const int MaxErrorChars = 500;

    public async Task RecordAsync(McpAuditRecord record, CancellationToken ct)
    {
        // A fresh scope means a fresh DbContext. The tool that just ran may have left tracked or
        // failed entities in the request's context, and they must not be saved along with the audit row.
        await using var scope = scopes.CreateAsyncScope();
        var auditDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        auditDb.McpAuditEntries.Add(new McpAuditEntry
        {
            TimestampUtc = time.GetUtcNow().UtcDateTime,
            UserId = record.UserId,
            UserName = record.UserName,
            ApiKeyId = record.ApiKeyId,
            AuthMethod = record.ApiKeyId is null ? "jwt" : "api-key",
            Kind = record.Kind,
            Target = Truncate(record.Target, 200)!,
            ArgumentsJson = Truncate(record.ArgumentsJson, MaxArgumentChars),
            Succeeded = record.Succeeded,
            Error = Truncate(record.Error, MaxErrorChars),
            DurationMs = record.DurationMs
        });

        await auditDb.SaveChangesAsync(ct);
    }

    public async Task<McpAuditPage> ListAsync(int page, int pageSize, string? kind, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.McpAuditEntries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(kind))
        {
            var wanted = kind.Trim().ToLowerInvariant();
            query = query.Where(e => e.Kind == wanted);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.TimestampUtc).ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new McpAuditDto(
                e.Id, e.TimestampUtc, e.UserId, e.UserName, e.ApiKeyId, e.AuthMethod,
                e.Kind, e.Target, e.ArgumentsJson, e.Succeeded, e.Error, e.DurationMs))
            .ToListAsync(ct);

        return new McpAuditPage(items, total, page, pageSize);
    }

    private static string? Truncate(string? text, int max) =>
        text is null || text.Length <= max ? text : text[..max] + "…";
}