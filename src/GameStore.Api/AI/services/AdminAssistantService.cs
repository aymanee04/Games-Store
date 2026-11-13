using GameStore.Api.AI.DTOs;
using GameStore.Api.AI.Interfaces;
using GameStore.Api.AI.Plugins;
using GameStore.Api.Exceptions;
using GameStore.Api.Services;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GameStore.Api.AI.services;

/// <summary>
/// Admin assistant, Semantic Kernel plugin style: the model chooses which plugin functions
/// to call (FunctionChoiceBehavior.Auto) and SK runs the tool loop. Read tools run directly.
/// Write tools only prepare a proposal, so the admin always confirms before anything is saved.
/// </summary>
public class AdminAssistantService(
    Kernel kernel,
    IOptions<OllamaOptions> options,
    IGameService games,
    IGenreService genreService,
    IStudioService studioService,
    ILogger<AdminAssistantService> logger) : IAdminAssistantService
{
    private readonly OllamaOptions _options = options.Value;

    private const string SystemPrompt = """
        You are the admin assistant of a game store. Answer in the language the admin writes in.
        Use the tools to look up data and to prepare changes. Never answer from memory.

        Rules:
        - To show or find games, call search_games. Report only what the tool returned.
        - To add one game, call propose_create_game. To change one game, call propose_update_game.
        - To change prices by a percentage, call propose_price_change. It finds the games and does
          the math itself, so do not search first and do not calculate prices yourself.
        - To delete one game, call propose_delete_game.
        - Act right away. Never ask the admin for permission before searching or preparing a change:
          proposals are only prepared for review and nothing is saved until the admin confirms.
        - Never say you cannot do something that one of your tools can do.
        - Pass only values the admin actually wrote. Never invent or guess a value.
          If something the tool needs is missing, ask the admin for it.
        - If a tool says a genre or studio does not exist, tell the admin plainly and
          mention the existing ones the tool returned. Do not substitute another one.
        - You cannot save anything. Never say that a change was saved or done.
        - Keep answers short.
        """;

    public async Task<AssistantResponse> HandleAsync(string message, CancellationToken ct)
    {
        var genres = (await genreService.GetAllAsync(ct)).Select(g => (g.Id, g.Name)).ToList();
        var studios = (await studioService.GetAllAsync(ct)).Select(s => (s.Id, s.Name)).ToList();
        var context = new AssistantContext(genres, studios);


        var requestKernel = kernel.Clone();
        requestKernel.Plugins.AddFromObject(new GameCatalogPlugin(games, context), "catalog");
        requestKernel.Plugins.AddFromObject(new GameChangePlugin(games, context), "changes");

        var history = new ChatHistory(SystemPrompt);
        history.AddUserMessage(message.Trim());

        // No JSON schema here: the model must be free to emit tool calls.
        var settings = OllamaPromptRunner.CreateSettings(_options, schema: null, maxTokens: 800, temperature: 0);
        settings.FunctionChoiceBehavior = FunctionChoiceBehavior.Auto();

        string reply;

        try
        {
            reply = await OllamaPromptRunner.RunChatAsync(requestKernel, history, settings, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Could not reach Ollama");
            throw new ServiceUnavailableException(
                "The AI service is not reachable. Make sure Ollama is running.");
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Ollama request timed out");
            throw new ServiceUnavailableException(
                "The AI service took too long to respond. Try a shorter request.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or KernelException)
        {
            logger.LogWarning(ex, "The assistant call failed");
            throw new BadRequestException("The assistant could not handle that. Try rephrasing it.");
        }

        logger.LogDebug("Assistant reply: {Reply}", reply);

        if (context.Proposals.Count > 0)
        {
            var text = string.IsNullOrWhiteSpace(reply)
                ? $"I prepared {context.Proposals.Count} change(s). Nothing is saved yet: review and confirm."
                : reply.Trim();

            return new AssistantResponse(
                AssistantResponseType.Proposal, text, context.Games, context.Proposals);
        }

        if (context.Games != null && context.Games.Count > 0 && string.IsNullOrWhiteSpace(reply))
        {
            var summary = context.Games.Count == 1
                ? $"I found {context.Games[0].Name}."
                : $"I found {context.Games.Count} games matching your request.";

            return new AssistantResponse(
                AssistantResponseType.Answer,
                summary,
                context.Games);
        }
        
        if (context.Games is not null && context.Games.Count == 0 && string.IsNullOrWhiteSpace(reply))
        {
            return new AssistantResponse(
                AssistantResponseType.Answer,
                $"I couldn't find this game.",
                context.Games);
        }

        return new AssistantResponse(
            AssistantResponseType.Answer,
            string.IsNullOrWhiteSpace(reply) ? "I couldn't produce an answer. Try rephrasing." : reply.Trim(),
            context.Games);
    }
}