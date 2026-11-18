using System.ComponentModel;
using ModelContextProtocol.Server;

namespace GameStore.Api.Mcp.Prompts;

/// <summary>
/// Prompts are reusable message templates that the USER picks (many clients show them as
/// slash commands). The server only fills in the arguments; the host sends the result to the model.
/// </summary>
[McpServerPromptType]
public sealed class GamePrompts
{
    [McpServerPrompt(Name = "price_review")]
    [Description("Review the prices of every game from one studio.")]
    public static string PriceReview(
        [Description("Studio name, as it exists in the store.")]
        string studio) =>
        $"""
         Review the prices of all games from the studio "{studio}" in the GameStore.
         1. Use the search_games tool with studio="{studio}" and read every page of results.
         2. List the games sorted by price, from the most expensive to the cheapest.
         3. Point out anything that looks unusual, for example a very high or very low price compared with the others.
         4. Do not change anything. Only report your findings.
         """;
}