using GameStore.Api.Dtos;

namespace GameStore.Api.Services;

public interface IGenreService
{
    Task<IReadOnlyList<GenreDto>> GetAllAsync(CancellationToken ct);
    Task<GenreDto> GetByIdAsync(int id, CancellationToken ct);
    Task<GenreDto> CreateAsync(CreateGenreRequest request, CancellationToken ct);
    Task UpdateAsync(int id, UpdateGenreRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}
