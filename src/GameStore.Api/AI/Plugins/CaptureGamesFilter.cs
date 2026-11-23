using System.Text.Json;
using GameStore.Shared.Dtos;
using Microsoft.SemanticKernel;

namespace GameStore.Api.AI.Plugins;

public sealed class CaptureGamesFilter(AssistantContext context) : IFunctionInvocationFilter
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext ctx,
        Func<FunctionInvocationContext, Task> next)
    {
        await next(ctx);

        if (ctx.Function.PluginName != "gamestore"
            || ctx.Function.Name != "search_games")
        {
            return;
        }

        var value = ctx.Result.GetValue<object>();
        var json = value switch
        {
            JsonElement element => element,
            string text => JsonDocument.Parse(text).RootElement,
            _ => JsonSerializer.SerializeToElement(value, JsonOptions)
        };

        var items = FindItems(json);
        if (items.ValueKind != JsonValueKind.Array)
            return;

        var games = items.Deserialize<List<GameDto>>(JsonOptions);
        if (games is null)
            return;

        var page = GetPage(ctx.Arguments, json);
        if (page <= 1 || context.Games is null)
            context.Games = games;
        else
            context.Games.AddRange(games);
    }

    private static JsonElement FindItems(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("items", out var items)
                || element.TryGetProperty("Items", out items))
            {
                return items;
            }

            foreach (var property in element.EnumerateObject())
            {
                var nested = FindItems(property.Value);
                if (nested.ValueKind != JsonValueKind.Undefined)
                    return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in element.EnumerateArray())
            {
                var nested = FindItems(entry);
                if (nested.ValueKind != JsonValueKind.Undefined)
                    return nested;
            }
        }

        return default;
    }

    private static int GetPage(
        KernelArguments arguments,
        JsonElement result)
    {
        if (arguments.TryGetValue("page", out var value)
            && int.TryParse(value?.ToString(), out var argumentPage))
        {
            return argumentPage;
        }

        if (result.ValueKind == JsonValueKind.Object
            && result.TryGetProperty("page", out var pageElement))
        {
            return pageElement.GetInt32();
        }

        return 1;
    }
}