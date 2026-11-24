using GameStore.Api.AI.DTOs;
using GameStore.Api.AI.Interfaces;
using GameStore.Api.Data;
using GameStore.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameStore.Api.AI.services;

/// <summary>
/// Chat history. The full text is stored; only the last few exchanges are
/// sent back to the model, shortened (every token is sent again with each request).
/// Every query filters by user id, so a conversation id alone never exposes someone's chat.
/// </summary>
public sealed class DbConversationStore(AppDbContext db) : IConversationStore
{
    private const int MemoryTurns = 3;         // exchanges replayed to the model
    private const int MemoryChars = 600;       // per message replayed to the model
    private const int MaxStoredChars = 8000;   // matches ChatMessageConfiguration
    private const int MaxTitleChars = 100;

    public async Task<IReadOnlyList<ChatTurn>> GetRecentAsync(
        string userId, string? conversationId, CancellationToken ct)
    {
        if (!Guid.TryParse(conversationId, out var id))
            return [];

        // Messages are saved in pairs (user, then assistant), so the newest
        // MemoryTurns * 2 rows are the last MemoryTurns exchanges.
        var messages = await db.ChatMessages.AsNoTracking()
            .Where(m => m.ConversationId == id && m.Conversation.UserId == userId)
            .OrderByDescending(m => m.Id)
            .Take(MemoryTurns * 2)
            .ToListAsync(ct);

        messages.Reverse();

        var turns = new List<ChatTurn>();

        for (var i = 0; i + 1 < messages.Count; i++)
        {
            if (messages[i].Role != "user" || messages[i + 1].Role != "assistant")
                continue;

            var reply = Shorten(messages[i + 1].Content);

            // Proposals are shown in the client, so tell the model they exist and are not saved.
            if (CountProposals(messages[i + 1].ProposalsJson) is > 0 and var count)
                reply += $" [{count} change proposal(s) prepared, waiting for the admin to confirm.]";

            turns.Add(new ChatTurn(Shorten(messages[i].Content), reply));
            i++;
        }

        return turns;
    }

    public async Task<string> AddAsync(
        string userId, string? conversationId, ChatTurn turn, string? proposalsJson, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        Conversation? conversation = null;

        if (Guid.TryParse(conversationId, out var id))
        {
            conversation = await db.Conversations
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);
        }

        // Unknown id, someone else's id, or no id: always a new conversation with a server-made id.
        if (conversation is null)
        {
            conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = Truncate(turn.User, MaxTitleChars),
                CreatedAtUtc = now
            };

            db.Conversations.Add(conversation);
        }

        conversation.UpdatedAtUtc = now;

        db.ChatMessages.Add(new ChatMessage
        {
            Conversation = conversation,
            Role = "user",
            Content = Truncate(turn.User, MaxStoredChars),
            CreatedAtUtc = now
        });

        db.ChatMessages.Add(new ChatMessage
        {
            Conversation = conversation,
            Role = "assistant",
            Content = Truncate(turn.Assistant, MaxStoredChars),
            ProposalsJson = proposalsJson,
            CreatedAtUtc = now
        });

        await db.SaveChangesAsync(ct);

        return conversation.Id.ToString("N");
    }

    private static string Shorten(string text) => Truncate(text, MemoryChars);

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";

    private static int CountProposals(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return 0;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array
                ? doc.RootElement.GetArrayLength()
                : 0;
        }
        catch (System.Text.Json.JsonException)
        {
            return 0;
        }
    }
}