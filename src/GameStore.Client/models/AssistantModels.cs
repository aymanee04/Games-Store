using GameStore.Shared.Dtos;

namespace GameStore.Client.Models;

/// <summary>What POST /api/ai/assistant/ask returns.</summary>
/// <param name="Type">"Answer" or "Proposal".</param>
public record AssistantResponse(
    string Type,
    string Message,
    List<GameDto>? Games,
    List<GameProposal>? Proposals);

/// <summary>
/// One change the assistant prepared. Nothing is saved until the admin confirms:
/// then Payload is sent to Method + Endpoint (the normal games endpoints).
/// </summary>
public record GameProposal(
    string Action,                    // "create" | "update"
    string Method,                    // "POST" | "PUT"
    string Endpoint,                  // "/api/games" | "/api/games/{id}"
    int? GameId,
    GameProposalPayload? Payload,
    List<ProposedChange> Changes);

public record GameProposalPayload(
    string Name,
    int GenreId,
    int StudioId,
    decimal Price,
    DateOnly ReleaseDate);

public record ProposedChange(string Field, string? Before, string After);