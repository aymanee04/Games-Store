using GameStore.Api.AI.DTOs;

namespace GameStore.Api.AI.Interfaces;

public interface IAdminAssistantService
{
    /// <summary>
    /// Understands what the admin wrote (read / create / update), runs reads immediately
    /// and turns writes into a proposal for review. Never writes to the database.
    /// The last few exchanges of the same conversation are remembered.
    /// </summary>
    Task<AssistantResponse> HandleAsync(
        string message, string userId, string? conversationId, string? provider, CancellationToken ct);

    /// <summary>The configured default provider, whether a client may override it, and the choices.</summary>
    AssistantProviderInfo GetProviderInfo();
}