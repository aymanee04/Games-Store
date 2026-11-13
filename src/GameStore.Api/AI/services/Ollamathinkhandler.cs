using System.Text;
using System.Text.Json.Nodes;
using GameStore.Api.AI.DTOs;
using Microsoft.Extensions.Options;

namespace GameStore.Api.AI.services;

/// <summary>
/// Adds the top-level "think" flag to Ollama chat requests. Semantic Kernel's Ollama
/// settings don't expose it, so we set it on the outgoing HTTP body instead.
/// Controlled by Ollama:Think (null = leave the request untouched).
/// </summary>
public sealed class OllamaThinkHandler(IOptions<OllamaOptions> options) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (options.Value.Think is { } think
            && request.Method == HttpMethod.Post
            && request.RequestUri?.AbsolutePath.EndsWith("/api/chat", StringComparison.OrdinalIgnoreCase) == true
            && request.Content is not null)
        {
            var json = await request.Content.ReadAsStringAsync(cancellationToken);

            if (JsonNode.Parse(json) is JsonObject body && !body.ContainsKey("think"))
            {
                body["think"] = think;
                request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}