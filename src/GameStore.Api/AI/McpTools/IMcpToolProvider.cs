using Microsoft.SemanticKernel;

namespace GameStore.Api.AI.McpTools;

public interface IMcpToolProvider
{
    /// <summary>
    /// The allowed MCP tools as Semantic Kernel functions. Empty when the feature is off, not
    /// configured, or the server cannot be reached (the caller then falls back to local tools).
    /// </summary>
    Task<IReadOnlyList<KernelFunction>> GetFunctionsAsync(CancellationToken ct);
}