using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using GameStore.Api.Mcp.Auth;
using ModelContextProtocol.Protocol;

namespace GameStore.Api.Mcp.Audit;

/// <summary>
/// Records every MCP tool call, resource read and prompt request: who (user, and API key if
/// one was used), what, with which arguments, how long, and whether it worked.
///
/// Register it BEFORE AddAuthorizationFilters(): resource and prompt requests that authorization
/// rejects then show up as failed entries. Tool calls rejected by authorization never reach the
/// ordinary call-tool filter this uses, so denied tool calls are not recorded here.
/// </summary>
public static class McpAuditFilters
{
    public static IMcpServerBuilder AddMcpAudit(this IMcpServerBuilder builder) =>
        builder.WithRequestFilters(filters =>
        {
            filters.AddCallToolFilter(next => async (context, ct) =>
            {
                var watch = Stopwatch.StartNew();
                CallToolResult? result = null;
                Exception? failure = null;

                try
                {
                    result = await next(context, ct);
                    return result;
                }
                catch (Exception ex)
                {
                    failure = ex;
                    throw;
                }
                finally
                {
                    // McpException and other tool errors normally come back as IsError results.
                    var error = failure?.Message
                                ?? (result?.IsError == true
                                    ? result.Content.OfType<TextContentBlock>().Select(b => b.Text).FirstOrDefault() ?? "error"
                                    : null);

                    await RecordAsync(
                        context.Services, context.User, "tool",
                        context.Params?.Name, context.Params?.Arguments, watch.Elapsed, error);
                }
            });

            filters.AddReadResourceFilter(next => async (context, ct) =>
            {
                var watch = Stopwatch.StartNew();
                Exception? failure = null;

                try
                {
                    return await next(context, ct);
                }
                catch (Exception ex)
                {
                    failure = ex;
                    throw;
                }
                finally
                {
                    await RecordAsync(
                        context.Services, context.User, "resource",
                        context.Params?.Uri, null, watch.Elapsed, failure?.Message);
                }
            });

            filters.AddGetPromptFilter(next => async (context, ct) =>
            {
                var watch = Stopwatch.StartNew();
                Exception? failure = null;

                try
                {
                    return await next(context, ct);
                }
                catch (Exception ex)
                {
                    failure = ex;
                    throw;
                }
                finally
                {
                    await RecordAsync(
                        context.Services, context.User, "prompt",
                        context.Params?.Name, context.Params?.Arguments, watch.Elapsed, failure?.Message);
                }
            });
        });

    private static async Task RecordAsync(
        IServiceProvider? services,
        ClaimsPrincipal? user,
        string kind,
        string? target,
        object? arguments,
        TimeSpan elapsed,
        string? error)
    {
        try
        {
            var audit = services?.GetService(typeof(IMcpAuditService)) as IMcpAuditService;
            if (audit is null)
                return;

            var keyId = Guid.TryParse(user?.FindFirstValue(McpApiKeyDefaults.KeyIdClaim), out var id)
                ? id
                : (Guid?)null;

            var record = new McpAuditRecord(
                UserId: user?.FindFirstValue("sub") ?? user?.FindFirstValue(ClaimTypes.NameIdentifier),
                UserName: user?.FindFirstValue(ClaimTypes.Name) ?? user?.FindFirstValue("unique_name"),
                ApiKeyId: keyId,
                Kind: kind,
                Target: string.IsNullOrWhiteSpace(target) ? "(unknown)" : target,
                ArgumentsJson: arguments is null ? null : JsonSerializer.Serialize(arguments),
                Succeeded: error is null,
                Error: error,
                DurationMs: (int)elapsed.TotalMilliseconds);

            // CancellationToken.None: the record must be written even if the client disconnected.
            await audit.RecordAsync(record, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Auditing must never break the call itself.
            (services?.GetService(typeof(ILoggerFactory)) as ILoggerFactory)
                ?.CreateLogger("McpAudit")
                .LogError(ex, "Could not write the MCP audit entry for {Kind} {Target}", kind, target);
        }
    }
}