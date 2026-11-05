namespace GameStore.Api.AI.services;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

public class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OllamaService(
        HttpClient httpClient,
        IOptions<OllamaOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<GameGenerationDto> GenerateGameAsync(
        string prompt,
        IReadOnlyList<string> genres,
        IReadOnlyList<string> studios,
        CancellationToken ct)
    {
        var systemPrompt = $"""
            You are a game catalog assistant.

            Your task is to generate a game proposal from the user's description.

            Rules:
            - Return only data matching the provided JSON schema.
            - Never invent genre names.
            - Select a genre from the available genres.
            - Select a studio from the available studios.
            - Use the exact names provided in the lists.
            - Price must be between 0 and 1000.
            - ReleaseDate must use YYYY-MM-DD format.
            - Do not invent database IDs.
            - If information is missing, make a reasonable proposal.

            Available genres:
            {JsonSerializer.Serialize(genres)}

            Available studios:
            {JsonSerializer.Serialize(studios)}
            """;

        var schema = new
        {
            type = "object",
            properties = new
            {
                name = new
                {
                    type = "string"
                },
                genreName = new
                {
                    type = "string"
                },
                studioName = new
                {
                    type = "string"
                },
                price = new
                {
                    type = "number"
                },
                releaseDate = new
                {
                    type = "string",
                    format = "date"
                }
            },
            required = new[]
            {
                "name",
                "genreName",
                "studioName",
                "price",
                "releaseDate"
            },
            additionalProperties = false
        };

        var requestBody = new
        {
            model = _options.Model,
            stream = false,
            format = schema,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = systemPrompt
                },
                new
                {
                    role = "user",
                    content = prompt
                }
            },
            options = new
            {
                temperature = 0.2
            }
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "api/chat",
            requestBody,
            ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
            JsonOptions,
            ct);

        var content = result?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException(
                "Ollama returned an empty response.");
        }

        return JsonSerializer.Deserialize<GameGenerationDto>(
                   content,
                   JsonOptions)
               ?? throw new InvalidOperationException(
                   "Could not deserialize the generated game.");
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}