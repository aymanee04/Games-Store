#pragma warning disable SKEXP0001 // AsKernelFunction is marked experimental
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;

namespace GameStore.Api.AI.McpTools;

/// <summary>
/// Owns the one MCP client connection the assistant uses. It is a singleton: the connection is
/// opened lazily on first use (the server is this very app, so it cannot connect while the app
/// is still starting) and the tool list is read once and reused.
/// </summary>
public sealed class McpToolProvider(
    IOptions<AssistantMcpOptions> options,
    ILoggerFactory loggerFactory) : IMcpToolProvider, IAsyncDisposable
{
    private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromSeconds(30);

    private readonly ILogger _logger = loggerFactory.CreateLogger<McpToolProvider>();
    private readonly SemaphoreSlim _gate = new(1, 1);

    private McpClient? _client;
    private IReadOnlyList<KernelFunction>? _functions;
    private DateTime _lastFailureUtc = DateTime.MinValue;

    public async Task<IReadOnlyList<KernelFunction>> GetFunctionsAsync(CancellationToken ct)
    {
        var settings = options.Value;

        if (!settings.Enabled)
            return [];

        if (_functions is not null)
            return _functions;

        await _gate.WaitAsync(ct);
        try
        {
            if (_functions is not null)
                return _functions;

            // After a failure, don't try again for a while: every assistant request would otherwise pay for it.
            if (DateTime.UtcNow - _lastFailureUtc < RetryAfterFailure)
                return [];

            if (string.IsNullOrWhiteSpace(settings.Endpoint) || string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                _logger.LogWarning("AssistantMcp is enabled but Endpoint or ApiKey is empty. Using the local tools.");
                _lastFailureUtc = DateTime.UtcNow;
                return [];
            }

            try
            {
                var transport = new HttpClientTransport(new HttpClientTransportOptions
                {
                    Endpoint = new Uri(settings.Endpoint),
                    TransportMode = HttpTransportMode.StreamableHttp,
                    AdditionalHeaders = new Dictionary<string, string>
                    {
                        ["Authorization"] = $"Bearer {settings.ApiKey}"
                    }
                }, loggerFactory);

                var client = await McpClient.CreateAsync(
                    transport, loggerFactory: loggerFactory, cancellationToken: ct);

                var offered = await client.ListToolsAsync(cancellationToken: ct);

                var allowed = offered
                    .Where(t => settings.AllowedTools.Contains(t.Name, StringComparer.Ordinal))
                    .ToList();

                var ignored = offered.Select(t => t.Name).Except(allowed.Select(t => t.Name)).ToList();

                _logger.LogInformation(
                    "Assistant MCP client connected: using [{Used}], ignoring [{Ignored}]",
                    string.Join(", ", allowed.Select(t => t.Name)),
                    string.Join(", ", ignored));

                if (allowed.Count == 0)
                {
                    _logger.LogWarning("The MCP server offers none of the allowed tools. Using the local tools.");
                    await client.DisposeAsync();
                    _lastFailureUtc = DateTime.UtcNow;
                    return [];
                }

                _client = client;
                _functions = allowed.Select(tool => tool.AsKernelFunction()).ToList();
                return _functions;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not connect the assistant to the MCP server at {Endpoint}. Using the local tools.",
                    settings.Endpoint);
                _lastFailureUtc = DateTime.UtcNow;
                return [];
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
            await _client.DisposeAsync();

        _gate.Dispose();
    }
}