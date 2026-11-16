using GameStore.Api.AI.DTOs;
using GameStore.Api.AI.Interfaces;
using GameStore.Api.AI.Plugins;
using GameStore.Api.Exceptions;
using GameStore.Api.Services;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GameStore.Api.AI.services;

public class AdminAssistantService(
    Kernel kernel,
    IGameService games,
    IGenreService genreService,
    IStudioService studioService,
    IConversationStore conversations,
    ILogger<AdminAssistantService> logger) : IAdminAssistantService
{
    private const string SystemPrompt = """
        You are the admin assistant of a game store. Answer in the language the admin writes in.
        Use the tools to look up data and to prepare changes. Never answer from memory.

        Rules:
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

    public async Task<AssistantResponse> HandleAsync(
        string message, string userId, string? conversationId, CancellationToken ct)
    {
        var chatId = string.IsNullOrWhiteSpace(conversationId)
            ? Guid.NewGuid().ToString("N")
            : conversationId.Trim();

        var genres = (await genreService.GetAllAsync(ct)).Select(g => (g.Id, g.Name)).ToList();
        var studios = (await studioService.GetAllAsync(ct)).Select(s => (s.Id, s.Name)).ToList();
        var context = new AssistantContext(genres, studios);

        var requestKernel = kernel.Clone();
        requestKernel.Plugins.AddFromObject(new GameCatalogPlugin(games, context), "catalog");
        requestKernel.Plugins.AddFromObject(new GameChangePlugin(games, context), "changes");

        var history = new ChatHistory(SystemPrompt);
        foreach (var turn in conversations.Get(userId, chatId))
        {
            history.AddUserMessage(turn.User);
            history.AddAssistantMessage(turn.Assistant);
        }

        history.AddUserMessage(message.Trim());

        var settings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        string reply;

        try
        {
            var chat = requestKernel.GetRequiredService<IChatCompletionService>();
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

        var remembered = context.Proposals.Count > 0
            ? $"{response.Message} [{context.Proposals.Count} change proposal(s) prepared, waiting for the admin to confirm.]"
            : response.Message;

        conversations.Add(userId, chatId, new ChatTurn(message.Trim(), remembered));

        return response with { ConversationId = chatId };
    }
}