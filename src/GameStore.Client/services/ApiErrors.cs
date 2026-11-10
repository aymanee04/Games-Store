using System.Net;
using System.Text.Json;

namespace GameStore.Client.Services;

public class ApiException(string message, HttpStatusCode? statusCode = null) : Exception(message)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
}

public static class ApiErrors
{
    public static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new ApiException(await ReadMessageAsync(response), response.StatusCode);
    }

    /// <summary>Turns the API's ProblemDetails / validation errors into one readable sentence.</summary>
    public static async Task<string> ReadMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();

            if (!string.IsNullOrWhiteSpace(body))
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    // Model validation: { "errors": { "Games[0].Name": ["..."] } }
                    if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                    {
                        var messages = new List<string>();
                        foreach (var field in errors.EnumerateObject())
                        {
                            if (field.Value.ValueKind != JsonValueKind.Array) continue;
                            foreach (var item in field.Value.EnumerateArray())
                            {
                                var text = item.GetString();
                                if (!string.IsNullOrWhiteSpace(text)) messages.Add(text);
                            }
                        }

                        if (messages.Count > 0) return string.Join(" ", messages);
                    }

                    // AppExceptions: { "title": "...", "detail": "..." }
                    if (root.TryGetProperty("detail", out var detail) &&
                        detail.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(detail.GetString()))
                        return detail.GetString()!;

                    if (root.TryGetProperty("title", out var title) &&
                        title.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(title.GetString()))
                        return title.GetString()!;
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON: fall through to the generic messages.
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Your session has expired. Please log in again.",
            HttpStatusCode.Forbidden => "You need an Admin account to do this.",
            _ => $"The request failed ({(int)response.StatusCode})."
        };
    }

    public static string Describe(Exception ex) => ex switch
    {
        ApiException api => api.Message,
        HttpRequestException => "Could not reach the server. Is the API running?",
        TaskCanceledException => "The request took too long and was cancelled.",
        _ => "Something went wrong. Please try again."
    };
}
