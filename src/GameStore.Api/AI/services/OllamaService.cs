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

    public OllamaService(
        HttpClient httpClient,
        ILogger<OllamaService> logger,
        IOptions<OllamaOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
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
                temperature = 0.1,
                num_predict = 2048
            }
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "api/chat",
            requestBody,
            ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(
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
    
    
    public async Task<List<ExtractedGameDto>> ExtractGamesAsync(
    string documentContent,
    IReadOnlyList<string> genres,
    IReadOnlyList<string> studios,
    CancellationToken ct)
{
    var systemPrompt = """
        You are a data extraction assistant for a game management system.

        Extract every game found in the provided document.

        Return only structured JSON matching the requested schema.

        Rules:
        - Extract only information present in the document.
        - Never invent missing information.
        - Use only the provided genre names.
        - Use only the provided studio names.
        - If a genre or studio cannot be matched, return an empty string.
        - If the price is missing, use 0.
        - Return releaseDate only in YYYY-MM-DD format.
        - If the date is missing, unclear, or invalid, return null.
        - Never invent a release date.
        - If the release date is missing or unclear, use null.
        - Ignore text that does not describe a game.
        - Do not create duplicate games from repeated document rows.
        - Process all games efficiently.
        - Return exactly one result for each valid game row.
        - Do not provide explanations or additional text.
        - Keep each field concise.
        - Return only the requested JSON.
        """;

    var userPrompt = $"""
        Available genres:
        {string.Join(", ", genres)}

        Available studios:
        {string.Join(", ", studios)}

        Extract the games from this document:

        {documentContent}
        """;

    var schema = new
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
                        genreName = new { type = "string" },
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

    var stopwatch = Stopwatch.StartNew();
    var response = await _httpClient.PostAsJsonAsync(
        "api/chat",
        new
        {
            model = _options.Model,
            stream = false,
            format = schema,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            options = new
            {
                temperature = 0.1,
                num_predict = 2048
            }
        },
        ct);
    _logger.LogInformation(
        "Ollama extraction request completed in {ElapsedSeconds:F2} seconds",
        stopwatch.Elapsed.TotalSeconds);

    response.EnsureSuccessStatusCode();

    var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(
        cancellationToken: ct);

    if (string.IsNullOrWhiteSpace(result?.Message?.Content))
        throw new InvalidOperationException(
            "Ollama returned an empty extraction result.");
    
    var extraction = JsonSerializer.Deserialize<GameExtractionResponse>(
        result.Message!.Content!,
        JsonOptions
    );

    if (extraction is null)
        throw new InvalidOperationException("Ollama returned an empty response.");

    var games = extraction.Games.Select(game =>
    {
        DateOnly? releaseDate = null;

        if (!string.IsNullOrWhiteSpace(game.ReleaseDate) &&
            DateOnly.TryParseExact(
                game.ReleaseDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate))
        {
            releaseDate = parsedDate;
        }

        return new ExtractedGameDto(
            game.Name,
            game.GenreName,
            game.StudioName,
            game.Price,
            releaseDate
        );
    }).ToList();

    return games;
}
    
    private sealed class OllamaResponse
    {
        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
    
    private sealed record OllamaExtractedGame(
        string Name,
        string GenreName,
        string StudioName,
        decimal Price,
        string? ReleaseDate
    );

    private sealed class GameExtractionResponse
    {
        public List<OllamaExtractedGame> Games { get; set; } = [];
    }
}


// info: System.Net.Http.HttpClient.IOllamaService.ClientHandler[101]
// Received HTTP response headers after 184470.7868ms - 200
// info: System.Net.Http.HttpClient.IOllamaService.LogicalHandler[101]
// End processing HTTP request after 184492.1696ms - 200
// info: GameStore.Api.AI.services.OllamaService[0]
// Ollama extraction request completed in 184.54 seconds
