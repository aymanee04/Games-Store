using GameStore.Api.AI.DTOs;

namespace GameStore.Api.AI.Interfaces;

public interface IAdminAssistantService
{
    /// <summary>
    /// Understands what the admin wrote (read / create / update), runs reads immediately
    /// and turns writes into a proposal for review. Never writes to the database.
    /// </summary>
    Task<AssistantResponse> HandleAsync(string message, CancellationToken ct);
}