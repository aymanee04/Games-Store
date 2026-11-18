using Microsoft.Extensions.AI;

namespace GameStore.Api.Entities;

public class Conversation
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;

    public string? Title { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public List<ChatMessage> Messages { get; set; } = [];
}