namespace GameStore.Api.Entities;

/// <summary>One message of a conversation: what the admin wrote, or the assistant's final reply.</summary>
public class ChatMessage
{
    public long Id { get; set; }
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    /// <summary>"user" or "assistant".</summary>
    public string Role { get; set; } = null!;

    public string Content { get; set; } = null!;

    /// <summary>
    /// Assistant messages only: the change proposals prepared in that reply (JSON), if any.
    /// A record of what was proposed. It does not say whether the admin confirmed it.
    /// </summary>
    public string? ProposalsJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}