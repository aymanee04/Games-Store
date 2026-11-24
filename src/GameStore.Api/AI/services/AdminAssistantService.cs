using System.Diagnostics;
using System.Text.Json;
using GameStore.Api.AI.DTOs;
using GameStore.Api.AI.Interfaces;
using GameStore.Api.AI.McpTools;
using GameStore.Api.AI.Plugins;
using GameStore.Api.Exceptions;
using GameStore.Api.Services;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using CaptureGamesFilter = GameStore.Api.AI.McpTools.CaptureGamesFilter;

namespace GameStore.Api.AI.services;

public class AdminAssistantService(
    Kernel kernel,
    IGameService games,
    IGenreService genreService,
    IStudioService studioService,
    IMcpToolProvider mcpTools,
    IConversationStore conversations,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<AdminAssistantService> logger) : IAdminAssistantService
{
    // The chat services registered in AddKernel(), by service id.
    private static readonly string[] Providers = ["ollama", "groq"];

    private const string SystemPrompt = """
        You are the admin assistant of a game store. Answer in the language the admin writes in.
        Use the tools to look up data and to prepare changes. Never answer from memory.

        Rules:
        - Don't answer about topics outside the gamestore. If the admin asks about them, say you cannot answer.
        - To show or find games, call search_games. Report only what the tool returned.
        - The admin sees search results as a table automatically. Never list the games in your
          reply: answer the question directly and briefly (how many, and any calculation the
          admin asked for, with the result).
        - Search results come in pages. When the question needs all the games (a count, a total,
          a comparison) and the tool says more pages remain, call search_games again with the
          same filters and the next pageNumber until you have read every page. Never request a
          page you already read. If you could not read every page, say that your answer covers
          only the games you saw.
        - To add one game, call propose_create_game. To change one game, call propose_update_game.
        - To change prices by a percentage, call propose_price_change. It finds the games and does
          the math itself, so do not search first and do not work out the new prices yourself.
        - To delete one game, call propose_delete_game.
        - Act right away. Never ask the admin for permission before searching or preparing a change:
          proposals are only prepared for review and nothing is saved until the admin confirms.
        - Never say you cannot do something that one of your tools can do.
        - Pass only values the admin actually wrote. Never invent or guess a value.
          If something the tool needs is missing, ask the admin for it.
        - If a tool says a genre or studio does not exist, tell the admin plainly and
          mention the existing ones the tool returned. Do not substitute another one.
        - You cannot save anything. Never say that a change was saved or done.
        - Earlier messages of this conversation may be included. Use them only to understand
          references such as "it" or "that game". Data can have changed since then, so always
          call the tools again instead of reusing earlier results.
        - Keep answers short.
        """;

    public AssistantProviderInfo GetProviderInfo() => new(
        Default: ResolveProvider(null),
        OverrideAllowed: environment.IsDevelopment(),
        Available: [.. Providers]);

    public async Task<AssistantResponse> HandleAsync(
        string message, string userId, string? conversationId, string? provider, CancellationToken ct)
    {
        var providerId = ResolveProvider(provider);

        var genres = (await genreService.GetAllAsync(ct)).Select(g => (g.Id, g.Name)).ToList();
        var studios = (await studioService.GetAllAsync(ct)).Select(s => (s.Id, s.Name)).ToList();
        var context = new AssistantContext(genres, studios);

        var requestKernel = kernel.Clone();

        // Read tools from MCP: this app's own /mcp endpoint, reached with a read-only API key.
        // The list is empty when the feature is off or the server is unreachable. Then the local
        // search tool below is used, so the assistant keeps working either way.
        var mcpFunctions = await mcpTools.GetFunctionsAsync(ct);
        var searchViaMcp = mcpFunctions.Any(f => f.Name == "search_games");

        var catalog = KernelPluginFactory.CreateFromObject(new GameCatalogPlugin(games, context), "catalog");

        // Never offer the model two search tools: drop the local one when MCP provides it.
        requestKernel.Plugins.Add(searchViaMcp
            ? KernelPluginFactory.CreateFromFunctions(
                catalog.Name, catalog.Description, catalog.Where(f => f.Name != "search_games"))
            : catalog);

        if (mcpFunctions.Count > 0)
        {
            requestKernel.Plugins.AddFromFunctions("gamestore", mcpFunctions);

            // MCP results go to the model only, so copy the games into the context for the client table.
            requestKernel.FunctionInvocationFilters.Add(new CaptureGamesFilter(context));
        }

        requestKernel.Plugins.AddFromObject(new GameChangePlugin(games, context), "changes");

        // The MCP search tool names its paging parameter "page" and returns JSON.
        var systemPrompt = searchViaMcp
            ? SystemPrompt.Replace("pageNumber", "page")
              + "\n        - The search_games tool returns JSON (items, totalCount, page, pageSize). Always pass pageSize=50 when you call it."
              + "\n        - The admin sees the games in a table next to your reply. Never paste the tool's JSON and never list the games: answer in one or two plain sentences (how many games, or the direct answer to the question)."
            : SystemPrompt;

        var history = new ChatHistory(systemPrompt);
        foreach (var turn in await conversations.GetRecentAsync(userId, conversationId, ct))
        {
            history.AddUserMessage(turn.User);
            history.AddAssistantMessage(turn.Assistant);
        }

        history.AddUserMessage(message.Trim());

        var settings = CreateSettings(providerId);

        string reply;
        var watch = Stopwatch.StartNew();

        try
        {
            var chat = requestKernel.GetRequiredService<IChatCompletionService>(providerId);
            var result = await chat.GetChatMessageContentAsync(history, settings, requestKernel, ct);
            reply = result.Content ?? "";
        }
        // The remote API answered with an error status (rate limit, bad key...).
        // Must come before the generic KernelException catch below.
        catch (HttpOperationException ex)
        {
            var status = (int?)ex.StatusCode;
            logger.LogError(ex, "The AI service returned HTTP {Status}", status);

            throw new ServiceUnavailableException(status switch
            {
                429 => "The AI service is busy (rate limit reached). Try again in a minute.",
                401 or 403 => "The AI service rejected the API key. Check the AI configuration.",
                _ => $"The AI service returned an error{(status is null ? "" : $" (HTTP {status})")}. Try again later."
            });
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Could not reach the AI service");
            throw new ServiceUnavailableException(
                "The AI service is not reachable. Check the network and the AI configuration.");
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "The AI request timed out");
            throw new ServiceUnavailableException(
                "The AI service took too long to respond. Try a shorter request.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or KernelException)
        {
            logger.LogWarning(ex, "The assistant call failed");
            throw new BadRequestException("The assistant could not handle that. Try rephrasing it.");
        }

        AssistantResponse response;

        if (context.Proposals.Count > 0)
        {
            var text = string.IsNullOrWhiteSpace(reply)
                ? $"I prepared {context.Proposals.Count} change(s). Nothing is saved yet: review and confirm."
                : reply.Trim();

            response = new AssistantResponse(
                AssistantResponseType.Proposal, text, context.Games, context.Proposals);
        }
        else
        {
            response = new AssistantResponse(
                AssistantResponseType.Answer,
                string.IsNullOrWhiteSpace(reply) ? "I couldn't produce an answer. Try rephrasing." : reply.Trim(),
                context.Games);
        }

        // Save the exchange (the store starts a new conversation when the id is missing or unknown).
        // The proposals are kept as a record of what the assistant suggested.
        var proposalsJson = context.Proposals.Count > 0
            ? JsonSerializer.Serialize(context.Proposals)
            : null;

        var savedId = await conversations.AddAsync(
            userId, conversationId, new ChatTurn(message.Trim(), response.Message), proposalsJson, ct);

        var answeredBy = $"{providerId}/{ModelOf(providerId)}";

        logger.LogInformation(
            "Assistant request answered by {AnsweredBy} in {Seconds:F1}s", answeredBy, watch.Elapsed.TotalSeconds);

        return response with { ConversationId = savedId, AnsweredBy = answeredBy };
    }

    // ---------------------------------------------------------------- provider switch
    // AI:Provider is the default. A client may choose another provider only in Development, so
    // nobody can pick a model in production. The result is always one of the registered ids.
    private string ResolveProvider(string? requested)
    {
        var configured = (configuration["AI:Provider"] ?? "").Trim().ToLowerInvariant();

        if (!Providers.Contains(configured))
            throw new InvalidOperationException(
                $"AI:Provider must be one of: {string.Join(", ", Providers)} (it is '{configured}').");

        if (!environment.IsDevelopment() || string.IsNullOrWhiteSpace(requested))
            return configured;

        var wanted = requested.Trim().ToLowerInvariant();

        return Providers.Contains(wanted)
            ? wanted
            : throw new BadRequestException(
                $"Unknown AI provider '{requested}'. Use one of: {string.Join(", ", Providers)}.");
    }

    private string? ModelOf(string providerId) =>
        configuration[providerId == "groq" ? "GROQ_MODEL" : "OLLAMA_MODEL"];

    // Tool calling and temperature 0 for both. Ollama also gets an explicit context size and
    // keep-alive: otherwise it may silently use a small default context that cuts the tools or
    // the history. Semantic Kernel passes these keys through to the Ollama request.
    private static PromptExecutionSettings CreateSettings(string providerId) => new()
    {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        ExtensionData = providerId == "ollama"
            ? new Dictionary<string, object>
            {
                ["temperature"] = 0,
                ["num_ctx"] = 4096,
                ["keep_alive"] = "30m"
            }
            : new Dictionary<string, object> { ["temperature"] = 0 }
    };
}