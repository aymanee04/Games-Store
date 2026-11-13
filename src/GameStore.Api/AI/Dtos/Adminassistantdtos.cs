using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using GameStore.Shared.Dtos;

namespace GameStore.Api.AI.DTOs;

public record AssistantRequest(
    [Required, StringLength(1000, MinimumLength = 3)]
    string Message);

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
    List<GameProposal>? Proposals = null);

/// <summary>
/// Everything the client needs to confirm a change through the EXISTING endpoints:
/// send <see cref="Payload"/> as the JSON body to <see cref="Method"/> <see cref="Endpoint"/>.
/// The assistant itself never writes to the database.
/// </summary>
public record GameProposal(
    string Action,                 // "create" | "update" | "delete"
    string Method,                 // "POST" | "PUT" | "DELETE"
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