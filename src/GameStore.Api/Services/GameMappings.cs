using System.Linq.Expressions;
using GameStore.Api.Dtos;
using GameStore.Api.Entities;

namespace GameStore.Api.Services;

public static class GameMappings
{
    /// <summary>Used inside queries so EF Core only selects the columns the DTO needs.</summary>
    public static readonly Expression<Func<Game, GameDto>> Projection =
        g => new GameDto(g.Id, g.Name, g.GenreId, g.Genre.Name, g.StudioId, g.Studio.Name, g.Price, g.ReleaseDate);

    /// <summary>For an entity already in memory (its Genre and Studio must be loaded).</summary>
    public static GameDto ToDto(this Game g) =>
        new(g.Id, g.Name, g.GenreId, g.Genre.Name, g.StudioId, g.Studio.Name, g.Price, g.ReleaseDate);
}
