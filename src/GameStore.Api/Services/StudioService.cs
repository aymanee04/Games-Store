using GameStore.Api.Data;
using GameStore.Api.Dtos;
using GameStore.Api.Entities;
using GameStore.Api.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GameStore.Api.Services;

public class StudioService(AppDbContext db, ILogger<StudioService> logger) : IStudioService
{
    public async Task<IReadOnlyList<StudioDtos>> GetAllAsync(CancellationToken ct) =>
        await db.Studios
            .AsNoTracking()
            .Select(s => new StudioDtos(s.Id, s.Name))
            .ToListAsync(ct);

    public async Task<StudioDtos> GetByIdAsync(int id, CancellationToken ct)
    {
        var studio = await db.Studios
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new StudioDtos(s.Id, s.Name))
            .FirstOrDefaultAsync(ct);

        return studio ?? throw new NotFoundException(nameof(Studio), id);
    }

    public async Task<StudioDtos> CreateAsync(CreateStudioRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();

        if (await db.Studios.AnyAsync(s => s.Name == name, ct))
        {
            throw new ConflictException($"Studio '{name}' already exists.");
        }

        var studio = new Studio { Name = name };
        db.Studios.Add(studio);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new ConflictException($"Studio '{name}' already exists.");
        }
 
        logger.LogInformation("Created studio {StudioId} ({StudioName})", studio.Id, studio.Name);
        return new StudioDtos(studio.Id, studio.Name);
    }

    public async Task UpdateAsync(int id, UpdateStudioRequest request, CancellationToken ct)
    {
        var studio = await db.Studios.FindAsync([id], ct)
                     ?? throw new NotFoundException(nameof(Studio), id);

        var name = request.Name.Trim();

        // Optional: Keep the fast pre-check to catch most cases early
        if (await db.Studios.AnyAsync(s => s.Name == name && s.Id != id, ct))
        {
            throw new ConflictException($"Studio '{name}' already exists.");
        }

        studio.Name = name;

        try
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Updated Studio {StudioId} ({StudioName})", studio.Id, studio.Name);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new ConflictException($"Studio '{name}' already exists.");
        }
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var studio = await db.Studios.FindAsync([id], ct)
            ?? throw new NotFoundException(nameof(Studio), id);

        if (await db.Games.AnyAsync(g => g.StudioId == id, ct))
        {
            throw new ConflictException("Cannot delete a studio that still has games.");
        }

        db.Studios.Remove(studio);
        await db.SaveChangesAsync(ct);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505";
    }
}
