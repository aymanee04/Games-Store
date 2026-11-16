using GameStore.Api.AI.DTOs;

namespace GameStore.Api.AI.Interfaces;

public interface IConversationStore
{
    IReadOnlyList<ChatTurn> Get(string userId, string conversationId);
 
    void Add(string userId, string conversationId, ChatTurn turn);
}