using GameStore.Api.Dtos;

namespace GameStore.Api.Services;

public interface IStudioService
{
    Task<IReadOnlyList<StudioDtos>> GetAllAsync(CancellationToken ct);
    Task<StudioDtos> GetByIdAsync(int id, CancellationToken ct);
    Task<StudioDtos> CreateAsync(CreateStudioRequest request, CancellationToken ct);
    Task UpdateAsync(int id, UpdateStudioRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}