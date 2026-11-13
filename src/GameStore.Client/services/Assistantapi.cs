using System.Net.Http.Json;
using GameStore.Client.Models;

namespace GameStore.Client.Services;

/// <summary>Admin-only. The bearer token is added by AuthTokenHandler.</summary>
public class AssistantApi(HttpClient http)
{
    public async Task<AssistantResponse> AskAsync(string message, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("api/ai/assistant/ask", new { message }, ct);
        await ApiErrors.EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<AssistantResponse>(cancellationToken: ct)
               ?? throw new ApiException("The server returned an empty response.");
    }

    /// <summary>Saves one proposal through the regular games endpoint it points to.</summary>
    public async Task ConfirmAsync(GameProposal proposal, CancellationToken ct = default)
    {
        // The assistant only ever proposes calls to the games endpoints.
        if (!proposal.Endpoint.StartsWith("/api/games", StringComparison.OrdinalIgnoreCase))
            throw new ApiException("This proposal points to an unexpected address, so it was not sent.");

        using var request = new HttpRequestMessage(
            new HttpMethod(proposal.Method), proposal.Endpoint.TrimStart('/'));

        if (proposal.Endpoint.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
        {
            using var deleteResponse = await http.SendAsync(request, ct);
            await ApiErrors.EnsureSuccessAsync(deleteResponse);
            return;
        }

        if (proposal.Payload is not null)
            request.Content = JsonContent.Create(proposal.Payload);

        using var response = await http.SendAsync(request, ct);
        await ApiErrors.EnsureSuccessAsync(response);
    }
}