using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using GameStore.Shared.Dtos;

namespace GameStore.Api.AI.DTOs;

public record AssistantRequest(
    [Required, StringLength(1000, MinimumLength = 3)]
    string Message,
    [StringLength(64)]
    string? ConversationId = null,   // omit to start a new chat; reuse the returned id to continue it
    [StringLength(20)]
    string? Provider = null);        // "ollama" or "groq". Only honored in Development; omit for the configured default

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AssistantResponseType
{
    /// <summary>The model's reply (an answer, or a question back to the admin). Games holds
    /// the structured result of the last catalog search, if one ran.</summary>
    Answer,

    /// <summary>A create/update is ready for the admin to review. Nothing was written.</summary>
    Proposal
}

public record AssistantResponse(
    AssistantResponseType Type,
    string Message,
    List<GameDto>? Games = null,
    List<GameProposal>? Proposals = null,
    string? ConversationId = null,
    string? AnsweredBy = null);   // for example "groq/openai/gpt-oss-20b"

/// <summary>
/// Everything the client needs to confirm a change through the EXISTING endpoints:
/// send <see cref="Payload"/> as the JSON body to <see cref="Method"/> <see cref="Endpoint"/>.
/// The assistant itself never writes to the database.
/// </summary>
public record GameProposal(
    string Action,                 // "create" | "update"
    string Method,                 // "POST" | "PUT"
    string Endpoint,               // "/api/games" | "/api/games/{id}"
    int? GameId,
    GameProposalPayload? Payload,
    List<ProposedChange> Changes); // what to show in the review screen

public record GameProposalPayload(
    string Name,
    int GenreId,
    int StudioId,
    decimal Price,
    DateOnly ReleaseDate);

public record ProposedChange(string Field, string? Before, string After);

/// <summary>What the page needs to show the provider switch.</summary>
public record AssistantProviderInfo(string Default, bool OverrideAllowed, List<string> Available);