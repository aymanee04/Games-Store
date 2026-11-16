using GameStore.Api.AI.Plugins;
using GameStore.Shared.Dtos;

namespace GameStore.Api.AI.Filters;

internal sealed record GameFilters(
    string? Name,
    string? Genre,
    string? Studio,
    decimal? MinPrice,
    decimal? MaxPrice,
    int? ReleasedAfterYear,
    int? ReleasedBeforeYear)
{
    public bool HasAny =>
        !string.IsNullOrWhiteSpace(Name) || !string.IsNullOrWhiteSpace(Genre)
        || !string.IsNullOrWhiteSpace(Studio) || MinPrice is not null || MaxPrice is not null
        || ReleasedAfterYear is not null || ReleasedBeforeYear is not null;
}

/// Turns what the model extracted (names) into the API's own GameQueryParameters (ids),
/// so reads and bulk changes go through IGameService like every other caller.
internal static class GameQueryBuilder
{
    public static GameQueryParameters? Build(
        AssistantContext context, GameFilters f, int pageSize, out string? error, int page = 1)
    {
        error = null;
        int? genreId = null;
        int? studioId = null;

        if (!string.IsNullOrWhiteSpace(f.Genre))
        {
            var found = AssistantHelpers.Find(context.Genres, f.Genre);
            if (found is null)
            {
                error = AssistantHelpers.NotFoundMessage("Genre", f.Genre, context.Genres);
                return null;
            }

            genreId = found.Value.Id;
        }

        if (!string.IsNullOrWhiteSpace(f.Studio))
        {
            var found = AssistantHelpers.Find(context.Studios, f.Studio);
            if (found is null)
            {
                error = AssistantHelpers.NotFoundMessage("Studio", f.Studio, context.Studios);
                return null;
            }

            studioId = found.Value.Id;
        }

        return new GameQueryParameters
        {
            Search = string.IsNullOrWhiteSpace(f.Name) ? null : f.Name.Trim(),
            GenreId = genreId,
            StudioId = studioId,
            MinPrice = f.MinPrice,
            MaxPrice = f.MaxPrice,
            ReleasedAfterYear = f.ReleasedAfterYear is { } a ? Math.Clamp(a, 1, 9999) : null,
            ReleasedBeforeYear = f.ReleasedBeforeYear is { } b ? Math.Clamp(b, 1, 9999) : null,
            Page = Math.Max(page, 1),
            PageSize = pageSize
        };
    }
}