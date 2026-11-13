using System.Globalization;
using GameStore.Api.AI.DTOs;
using GameStore.Api.validations;
using GameStore.Shared.Dtos;

namespace GameStore.Api.AI.Plugins;

/// <summary>
/// One instance per assistant request. Plugins read the lookups from it and leave their
/// structured results here, so the API can return real data and not only the model's text.
/// </summary>
public sealed class AssistantContext(
    List<(int Id, string Name)> genres,
    List<(int Id, string Name)> studios)
{
    public List<(int Id, string Name)> Genres { get; } = genres;
    public List<(int Id, string Name)> Studios { get; } = studios;

    /// <summary>Filled by the propose_* functions. One entry per game to change.</summary>
    public List<GameProposal> Proposals { get; } = [];

    /// <summary>Set by search_games.</summary>
    public List<GameDto>? Games { get; set; }
}

internal static class AssistantHelpers
{
    public static (int Id, string Name)? Find(List<(int Id, string Name)> items, string requested)
    {
        var match = items.FirstOrDefault(i =>
            string.Equals(i.Name, requested.Trim(), StringComparison.OrdinalIgnoreCase));

        return match.Name is null ? null : match;
    }

    public static string NotFoundMessage(
        string kind, string requested, List<(int Id, string Name)> existing)
    {
        var list = existing.Count == 0
            ? "none yet"
            : string.Join(", ", existing.Select(e => e.Name).Take(15));

        return $"{kind} '{requested.Trim()}' doesn't exist in the store. "
               + $"Existing {kind.ToLowerInvariant()}s: {list}.";
    }
    

    public static bool TryParseDate(string text, out DateOnly date) =>
        DateOnly.TryParseExact(
            text.Trim(), "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static bool IsValidReleaseDate(DateOnly d) =>
        new ValidReleaseDateAttribute().IsValid(d);

    public static string FormatDate(DateOnly d) =>
        d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string FormatPrice(decimal p) =>
        p.ToString("0.##", CultureInfo.InvariantCulture);

}