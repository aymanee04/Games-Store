using System.Text.Json;
using GameStore.Api.AI.Plugins;
using GameStore.Shared.Dtos;
using Microsoft.SemanticKernel;

namespace GameStore.Api.AI.McpTools;

/// <summary>
/// A tool that comes over MCP returns its result only to the model. The client table, however,
/// shows exact database rows from AssistantContext.Games. This filter runs after every function
/// call, and for the MCP search_games call it reads the games out of the result and stores them
/// in the context, as the local plugin did.
/// </summary>
public sealed class CaptureGamesFilter(AssistantContext context) : IFunctionInvocationFilter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext ctx, Func<FunctionInvocationContext, Task> next)
    {
        await next(ctx);

        if (ctx.Function.PluginName != "gamestore" || ctx.Function.Name != "search_games")
            return;

        var value = ctx.Result.GetValue<object>();
        var games = ReadGames(value);

        if (games is null)
        {
            ctx.Kernel.Services.GetService<ILoggerFactory>()?
                .CreateLogger<CaptureGamesFilter>()
                .LogWarning(
                    "Could not read the games out of the MCP search_games result (type {Type}), so no table is shown.",
                    value?.GetType().FullName ?? "null");
            return;
        }

        // Same rule as the local plugin: page 1 starts the list, later pages add to it.
        var page = 1;
        if (ctx.Arguments.TryGetValue("page", out var argument) && int.TryParse(argument?.ToString(), out var parsed))
            page = parsed;

        if (page <= 1 || context.Games is null)
            context.Games = games;
        else
            context.Games.AddRange(games);

        ctx.Kernel.Services.GetService<ILoggerFactory>()?
            .CreateLogger<CaptureGamesFilter>()
            .LogInformation(
                "Captured {Count} game(s) from MCP search_games (page {Page}); the table now holds {Total}.",
                games.Count, page, context.Games.Count);
    }

    // What an MCP tool call returns depends on the result: for a simple text result the SDK hands
    // over a Microsoft.Extensions.AI TextContent (or a list of them), and only for richer results
    // (structured content, errors) a JsonElement holding the whole tool result.
    // Microsoft.Extensions.AI.TextContent is written in full: Semantic Kernel has a type with the same name.
    private static List<GameDto>? ReadGames(object? value)
    {
        try
        {
            switch (value)
            {
                case null:
                    return null;

                case Microsoft.Extensions.AI.TextContent text:
                    return FromJsonText(text.Text);

                case string text:
                    return FromJsonText(text);

                case JsonElement element:
                    return FromElement(element);

                case IEnumerable<Microsoft.Extensions.AI.AIContent> parts:
                    foreach (var part in parts)
                    {
                        if (part is Microsoft.Extensions.AI.TextContent partText && FromJsonText(partText.Text) is { } games)
                            return games;
                    }

                    return null;

                default:
                    return FromElement(JsonSerializer.SerializeToElement(value));
            }
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static List<GameDto>? FromJsonText(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : FromElement(JsonDocument.Parse(text).RootElement);

    private static List<GameDto>? FromElement(JsonElement root)
    {
        // { "structuredContent": { "items": [...] } }
        if (TryGet(root, "structuredContent", out var structured) && TryGet(structured, "items", out var structuredItems))
            return structuredItems.Deserialize<List<GameDto>>(Json);

        // { "content": [ { "type": "text", "text": "{ \"items\": [...] }" } ] }
        if (TryGet(root, "content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                if (TryGet(block, "text", out var blockText) && blockText.ValueKind == JsonValueKind.String
                    && FromJsonText(blockText.GetString()) is { } games)
                    return games;
            }
        }

        // a serialized TextContent: { "text": "{ \"items\": [...] }", ... }
        if (TryGet(root, "text", out var text) && text.ValueKind == JsonValueKind.String
            && FromJsonText(text.GetString()) is { } fromText)
            return fromText;

        // the paged result itself: { "items": [...], "totalCount": ... }
        if (TryGet(root, "items", out var items))
            return items.Deserialize<List<GameDto>>(Json);

        return null;
    }

    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value);
    }
}