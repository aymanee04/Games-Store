using GameStore.Api.Data;
using GameStore.Api.Dtos;
using GameStore.Api.Entities;
using GameStore.Api.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace GameStore.Api.Services;

public class GameService(AppDbContext db, ILogger<GameService> logger) : IGameService
{
    public async Task<PagedResult<GameDto>> GetAllAsync(GameQueryParameters parameters, CancellationToken ct)
    {
        // AsNoTracking: read-only query, no change-tracking overhead.
        var query = db.Games.AsNoTracking();

        if (parameters.GenreId is not null)
        {
            query = query.Where(g => g.GenreId == parameters.GenreId);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var term = parameters.Search.Trim();
            query = query.Where(g => EF.Functions.ILike(g.Name, $"%{term}%"));   // PostgreSQL case-insensitive LIKE
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(g => g.Name).ThenBy(g => g.Id)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(GameMappings.Projection)
            .ToListAsync(ct);

        return new PagedResult<GameDto>(items, parameters.Page, parameters.PageSize, total);
    }

    public async Task<GameDto> GetByIdAsync(int id, CancellationToken ct)
    {
        var game = await db.Games
            .AsNoTracking()
            .Where(g => g.Id == id)
            .Select(GameMappings.Projection)
            .FirstOrDefaultAsync(ct);

        return game ?? throw new NotFoundException(nameof(Game), id);
    }

    public async Task<GameDto> CreateAsync(CreateGameRequest request, CancellationToken ct)
    {
        var genre = await db.Genres.FindAsync([request.GenreId], ct)
            ?? throw new BadRequestException($"Genre with id '{request.GenreId}' does not exist.");
       
        var studio = await db.Studios.FindAsync([request.StudioId], ct)
            ?? throw new BadRequestException($"Studio with id '{request.StudioId}' does not exist.");

        var game = new Game
        {
            Name = request.Name.Trim(),
            Genre = genre,
            Studio = studio,
            Price = request.Price,
            ReleaseDate = request.ReleaseDate
        };

        db.Games.Add(game);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created game {GameId} ({GameName})", game.Id, game.Name);
        return game.ToDto();
    }

    public async Task UpdateAsync(int id, UpdateGameRequest request, CancellationToken ct)
    {
        var game = await db.Games.FindAsync([id], ct)
            ?? throw new NotFoundException(nameof(Game), id);
        
        if (game.GenreId != request.GenreId && !await db.Genres.AnyAsync(g => g.Id == request.GenreId, ct))
        {
            throw new BadRequestException($"Genre with id '{request.GenreId}' does not exist.");
        }
        
        if (game.StudioId != request.StudioId && !await db.Studios.AnyAsync(g => g.Id == request.StudioId, ct))
        {
            throw new BadRequestException($"Studio with id '{request.StudioId}' does not exist.");
        }
        
        // The entity is tracked, so SaveChanges generates the UPDATE automatically.
        game.Name = request.Name.Trim();
        game.GenreId = request.GenreId;
        game.StudioId = request.StudioId;
        game.Price = request.Price;
        game.ReleaseDate = request.ReleaseDate;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Updated game {GameId}", id);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var deleted = await db.Games.Where(g => g.Id == id).ExecuteDeleteAsync(ct);

        if (deleted == 0)
        {
            throw new NotFoundException(nameof(Game), id);
        }

        logger.LogInformation("Deleted game {GameId}", id);
    }
}
