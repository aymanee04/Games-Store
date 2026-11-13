using System.Text.Json;
using GameStore.Api.AI.DTOs;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GameStore.Api.AI.services;

/// <summary>
/// Shared plumbing for talking to Ollama through Semantic Kernel. Used by OllamaService
/// (import, structured JSON) and AdminAssistantService (chat with plugins).
/// </summary>
internal static class OllamaPromptRunner
{
    // Ollama-specific knobs travel in ExtensionData; the connector maps them to the
    // Ollama request (format = JSON schema, options.num_ctx, keep_alive...).
    // schema == null -> no JSON constraint (needed when the model must call tools).
    public static PromptExecutionSettings CreateSettings(
        OllamaOptions options, object? schema, int maxTokens = 2048, double temperature = 0.1)
    {
        var extension = new Dictionary<string, object>
        {
            ["temperature"] = temperature,
            ["max_tokens"] = maxTokens,                  // -> num_predict
            ["num_ctx"] = options.NumCtx,
            ["keep_alive"] = options.KeepAlive
        };

        if (schema is not null)
            extension["response_format"] = JsonSerializer.SerializeToElement(schema);

        if (options.NumThread is { } threads)
            extension["num_thread"] = threads;

        return new PromptExecutionSettings { ExtensionData = extension };
    }

    
    /// <summary>
    /// Chat completion. With FunctionChoiceBehavior.Auto() in the settings, SK runs the
    /// call -> tool -> call loop itself and returns the model's final text.
    /// </summary>
    public static async Task<string> RunChatAsync(
        Kernel kernel,
        ChatHistory history,
        PromptExecutionSettings settings,
        CancellationToken ct)
    {
        var chat = kernel.GetRequiredService<IChatCompletionService>();

        var result = await Guard(
            () => chat.GetChatMessageContentAsync(history, settings, kernel, ct), ct);

        return result.Content ?? "";
    }

    // SK may wrap transport errors; callers already map these two exception types
    // to friendly 503 messages, so unwrap them here.
    private static async Task<T> Guard<T>(Func<Task<T>> action, CancellationToken ct)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is not OperationCanceledException
                                   && ex.GetBaseException() is HttpRequestException http)
        {
            throw new HttpRequestException(http.Message, ex);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested
                                   && ex.GetBaseException() is TaskCanceledException or TimeoutException)
        {
            throw new TaskCanceledException("The AI request timed out.", ex);
        }
    }
}