using GameStore.Api.Data;
using GameStore.Api.Dtos;
using GameStore.Api.Entities;
using GameStore.Api.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace GameStore.Api.Services;

public class GenreService(AppDbContext db, ILogger<GenreService> logger) : IGenreService
{
    public async Task<IReadOnlyList<GenreDto>> GetAllAsync(CancellationToken ct) =>
        await db.Genres
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .Select(g => new GenreDto(g.Id, g.Name))
            .ToListAsync(ct);

    public async Task<GenreDto> GetByIdAsync(int id, CancellationToken ct)
    {
        var genre = await db.Genres
            .AsNoTracking()
            .Where(g => g.Id == id)
            .Select(g => new GenreDto(g.Id, g.Name))
            .FirstOrDefaultAsync(ct);

        return genre ?? throw new NotFoundException(nameof(Genre), id);
    }

    public async Task<GenreDto> CreateAsync(CreateGenreRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        
        var genre = new Genre { Name = name };
        db.Genres.Add(genre);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created genre {GenreId} ({GenreName})", genre.Id, genre.Name);
        return new GenreDto(genre.Id, genre.Name);
    }

    public async Task UpdateAsync(int id, UpdateGenreRequest request, CancellationToken ct)
    {
        var genre = await db.Genres.FindAsync([id], ct)
            ?? throw new NotFoundException(nameof(Genre), id);

        var name = request.Name.Trim();

        if (await db.Genres.AnyAsync(g => g.Name == name && g.Id != id, ct))
        {
            throw new ConflictException($"Genre '{name}' already exists.");
        }

        genre.Name = name;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var genre = await db.Genres.FindAsync([id], ct)
            ?? throw new NotFoundException(nameof(Genre), id);

        if (await db.Games.AnyAsync(g => g.GenreId == id, ct))
        {
            throw new ConflictException("Cannot delete a genre that still has games.");
        }

        db.Genres.Remove(genre);
        await db.SaveChangesAsync(ct);
    }
}
