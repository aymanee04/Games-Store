using System.Diagnostics;
using System.Globalization;
using GameStore.Api.AI.DTOs;
using GameStore.Api.AI.Interfaces;

namespace GameStore.Api.AI.services;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

public class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Same JSON shape for extraction and generation: { "games": [ {...}, {...} ] }
    // When allowedGenres is given, the model is physically unable to return any
    // genre outside that list (JSON-schema enum enforced by Ollama's decoder).
    private static object BuildGamesSchema(IReadOnlyList<string>? allowedGenres = null)
    {
        object genreName = allowedGenres is { Count: > 0 }
            ? new { type = "string", @enum = allowedGenres }
            : new { type = "string" };

        return new
        {
            type = "object",
            properties = new
            {
                games = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string" },
                            genreName,
                            studioName = new { type = "string" },
                            price = new { type = "number" },
                            releaseDate = new
                            {
                                type = new[] { "string", "null" }
                            }
                        },
                        required = new[]
                        {
                            "name", "genreName", "studioName",
                            "price", "releaseDate"
                        }
                    }
                }
            },
            required = new[] { "games" }
        };
    }

    public OllamaService(
        HttpClient httpClient,
        ILogger<OllamaService> logger,
        IOptions<OllamaOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }
    
    // ---------- Create games from a free-text prompt ----------
    public async Task<List<ExtractedGameDto>> GenerateGamesAsync(
        string prompt,
        IReadOnlyList<string> genres,
        IReadOnlyList<string> studios,
        CancellationToken ct)
    {
        var systemPrompt = $"""
            You are a game catalog assistant for a game store.

            The user describes one or more games they want to add to the catalog.
            Turn the description into structured game entries.

            Rules:
            - Return only JSON matching the requested schema, with no extra text.
            - Return exactly the number of games the user asks for. If no number is given, return 1.
            - Use the name the user gives. If no name is given, propose a fitting original name.
            - genreName: MUST be exactly one value from the available genres list.
              Never invent or combine genres. If the user describes a genre that is not in the list
              (for example "action RPG"), choose the closest genre from the list.
            - studioName: pick from the available studios, using the exact spelling from the list.
            - If the user explicitly names a studio that is NOT in the studios list,
              return it exactly as the user wrote it. Do not replace it with another one.
            - If the user does not mention a genre or studio, pick the best match from the lists.
            - price: use the price the user gives. Otherwise propose a reasonable price
              between 0 and 1000 (a number, no currency symbol).
            - releaseDate: YYYY-MM-DD. If the user gives only a year, use January 1st of that year.
              If no date is given, propose a plausible date.
            - Do not invent database IDs.

            Available genres:
            {JsonSerializer.Serialize(genres)}

            Available studios:
            {JsonSerializer.Serialize(studios)}
            """;

        var content = await SendChatAsync(
            systemPrompt, prompt, "generation", BuildGamesSchema(genres), ct);

        return ParseGames(content);
    }

    // ---------- Shared helpers ----------
    private async Task<string> SendChatAsync(
        string systemPrompt,
        string userPrompt,
        string operation,
        object schema,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        var modelOptions = new Dictionary<string, object>
        {
            ["temperature"] = 0.1,
            ["num_predict"] = 2048,
            ["num_ctx"] = _options.NumCtx
        };

        if (_options.NumThread is { } threads)
            modelOptions["num_thread"] = threads;

        using var response = await _httpClient.PostAsJsonAsync(
            "api/chat",
            new
            {
                model = _options.Model,
                stream = false,
                format = schema,
                keep_alive = _options.KeepAlive,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                options = modelOptions
            },
            ct);

        _logger.LogInformation(
            "Ollama {Operation} request completed in {ElapsedSeconds:F2} seconds",
            operation,
            stopwatch.Elapsed.TotalSeconds);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(
            JsonOptions, ct);

        if (result is not null)
        {
            const double nsPerSecond = 1_000_000_000d;

            var tokensPerSecond = result.EvalDuration > 0
                ? result.EvalCount / (result.EvalDuration / nsPerSecond)
                : 0;

            _logger.LogInformation(
                "Ollama {Operation} breakdown: model load {Load:F1}s, " +
                "prompt {PromptTokens} tokens in {Prompt:F1}s, " +
                "output {OutputTokens} tokens in {Output:F1}s ({Tps:F1} tokens/s)",
                operation,
                result.LoadDuration / nsPerSecond,
                result.PromptEvalCount,
                result.PromptEvalDuration / nsPerSecond,
                result.EvalCount,
                result.EvalDuration / nsPerSecond,
                tokensPerSecond);

            if (result.DoneReason == "length")
                _logger.LogWarning(
                    "Ollama {Operation} output was cut off at the token limit",
                    operation);
        }

        var content = result?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException(
                $"Ollama returned an empty {operation} result.");

        return content;
    }

    private static List<ExtractedGameDto> ParseGames(string json)
    {
        var extraction = JsonSerializer.Deserialize<GameExtractionResponse>(
                             json, JsonOptions)
                         ?? throw new InvalidOperationException(
                             "Ollama returned an empty response.");

        return (extraction.Games ?? [])
            .Where(g => !string.IsNullOrWhiteSpace(g.Name))
            .Select(g =>
            {
                DateOnly? releaseDate = null;

                if (!string.IsNullOrWhiteSpace(g.ReleaseDate) &&
                    DateOnly.TryParseExact(
                        g.ReleaseDate,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var parsedDate))
                {
                    releaseDate = parsedDate;
                }

                return new ExtractedGameDto(
                    g.Name!.Trim(),
                    g.GenreName?.Trim() ?? "",
                    g.StudioName?.Trim() ?? "",
                    g.Price,
                    releaseDate);
            })
            .ToList();
    }

    private sealed class OllamaResponse
    {
        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }

        [JsonPropertyName("done_reason")]
        public string? DoneReason { get; set; }

        [JsonPropertyName("load_duration")]
        public long LoadDuration { get; set; }

        [JsonPropertyName("prompt_eval_count")]
        public int PromptEvalCount { get; set; }

        [JsonPropertyName("prompt_eval_duration")]
        public long PromptEvalDuration { get; set; }

        [JsonPropertyName("eval_count")]
        public int EvalCount { get; set; }

        [JsonPropertyName("eval_duration")]
        public long EvalDuration { get; set; }
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private sealed record OllamaExtractedGame(
        string? Name,
        string? GenreName,
        string? StudioName,
        decimal Price,
        string? ReleaseDate
    );

    private sealed class GameExtractionResponse
    {
        public List<OllamaExtractedGame>? Games { get; set; } = [];
    }
}