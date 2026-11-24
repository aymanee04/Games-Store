using GameStore.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameStore.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Games.AnyAsync(ct))
        {
            return;
        }

        var capcom = new Studio { Name = "capcom" };
        var squareEnix = new Studio { Name = "square enix" };
        var eaSports = new Studio { Name = "ea sports" };
        var playgroundGames = new Studio { Name = "playground games" };

        db.Studios.AddRange(capcom, squareEnix, eaSports, playgroundGames);

        var fighting = new Genre { Name = "fighting" };
        var rpg = new Genre { Name = "roleplaying" };
        var sports = new Genre { Name = "sports" };
        var racing = new Genre { Name = "racing" };

        db.Genres.AddRange(fighting, rpg, sports, racing);

        db.Games.AddRange(
            new Game
            {
                Name = "street fighter ii",
                Genre = fighting,
                Studio = capcom,
                Price = 19.99m,
                ReleaseDate = new DateOnly(1992, 7, 15)
            },
            new Game
            {
                Name = "final fantasy xiv",
                Genre = rpg,
                Studio = squareEnix,
                Price = 59.99m,
                ReleaseDate = new DateOnly(2010, 9, 30)
            },
            new Game
            {
                Name = "fifa 23",
                Genre = sports,
                Studio = eaSports,
                Price = 69.99m,
                ReleaseDate = new DateOnly(2022, 9, 27)
            },
            new Game
            {
                Name = "forza horizon 5",
                Genre = racing,
                Studio = playgroundGames,
                Price = 59.99m,
                ReleaseDate = new DateOnly(2021, 11, 9)
            });

        await db.SaveChangesAsync(ct);
    }
}