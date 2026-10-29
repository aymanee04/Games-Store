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

        var capcom = new Studio { Name = "Capcom" };
        var squareEnix = new Studio { Name = "Square Enix" };
        var eaSports = new Studio { Name = "EA Sports" };
        var playgroundGames = new Studio { Name = "Playground Games" };

        db.Studios.AddRange(capcom, squareEnix, eaSports, playgroundGames);

        var fighting = new Genre { Name = "Fighting" };
        var rpg = new Genre { Name = "Roleplaying" };
        var sports = new Genre { Name = "Sports" };
        var racing = new Genre { Name = "Racing" };

        db.Genres.AddRange(fighting, rpg, sports, racing);

        db.Games.AddRange(
            new Game
            {
                Name = "Street Fighter II",
                Genre = fighting,
                Studio = capcom,
                Price = 19.99m,
                ReleaseDate = new DateOnly(1992, 7, 15)
            },
            new Game
            {
                Name = "Final Fantasy XIV",
                Genre = rpg,
                Studio = squareEnix,
                Price = 59.99m,
                ReleaseDate = new DateOnly(2010, 9, 30)
            },
            new Game
            {
                Name = "FIFA 23",
                Genre = sports,
                Studio = eaSports,
                Price = 69.99m,
                ReleaseDate = new DateOnly(2022, 9, 27)
            },
            new Game
            {
                Name = "Forza Horizon 5",
                Genre = racing,
                Studio = playgroundGames,
                Price = 59.99m,
                ReleaseDate = new DateOnly(2021, 11, 9)
            });

        await db.SaveChangesAsync(ct);
    }
}