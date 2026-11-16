using GameStore.Api.AI.DTOs;
using GameStore.Api.AI.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace GameStore.Api.AI.services;


public sealed class ConversationStore(IMemoryCache cache) : IConversationStore
{
    private const int MaxTurns = 3;
    private const int MaxChars = 600;
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    // The user id is part of the key, so one admin can never read another's conversation.
    private static string Key(string userId, string conversationId) =>
        $"assistant-chat:{userId}:{conversationId}";

    public IReadOnlyList<ChatTurn> Get(string userId, string conversationId) =>
        cache.TryGetValue(Key(userId, conversationId), out List<ChatTurn>? turns) && turns is not null
            ? turns
            : [];

    public void Add(string userId, string conversationId, ChatTurn turn)
    {
        var turns = Get(userId, conversationId)
            .Append(new ChatTurn(Truncate(turn.User), Truncate(turn.Assistant)))
            .TakeLast(MaxTurns)
            .ToList();

        cache.Set(
            Key(userId, conversationId),
            turns,
            new MemoryCacheEntryOptions { SlidingExpiration = IdleTimeout });
    }

    private static string Truncate(string text) =>
        text.Length <= MaxChars ? text : text[..MaxChars] + "…";
}