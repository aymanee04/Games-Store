using System.Diagnostics;
using Microsoft.SemanticKernel;

namespace GameStore.Api.AI.Filters;

public sealed class AiTimingFilter(ILogger<AiTimingFilter> logger) : IFunctionInvocationFilter
{
    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        finally
        {
            logger.LogInformation(
                "AI function {Function} finished in {Seconds:F1}s",
                context.Function.Name,
                stopwatch.Elapsed.TotalSeconds);
        }

        // Whatever the connector exposes (token counts, durations...). Look at this once
        // in the console to see what's available, then promote what you need to Information.
        if (context.Result.Metadata is { Count: > 0 } metadata && logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "AI function {Function} metadata: {Metadata}",
                context.Function.Name,
                string.Join(", ", metadata.Select(kv => $"{kv.Key}={kv.Value}")));
        }
    }
}