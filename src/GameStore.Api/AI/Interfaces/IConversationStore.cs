using GameStore.Api.AI.DTOs;

namespace GameStore.Api.AI.Interfaces;

/// <summary>Persistent assistant chat history. Every call is scoped to one user.</summary>
public interface IConversationStore
{
    /// <summary>
    /// The last few exchanges of a conversation, oldest first, ready to send to the model.
    /// Empty if the id is missing, invalid, unknown, or belongs to another user.
    /// </summary>
    Task<IReadOnlyList<ChatTurn>> GetRecentAsync(
        string userId, string? conversationId, CancellationToken ct);

    /// <summary>
    /// Saves one exchange. Continues the conversation if it exists for this user, otherwise
    /// starts a new one. Returns the id of the conversation the exchange was saved to.
    /// </summary>
    Task<string> AddAsync(
        string userId, string? conversationId, ChatTurn turn, string? proposalsJson, CancellationToken ct);
}