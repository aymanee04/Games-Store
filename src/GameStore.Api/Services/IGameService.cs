using GameStore.Api.Dtos;

namespace GameStore.Api.Services;

public interface IGameService
{
    Task<PagedResult<GameDto>> GetAllAsync(GameQueryParameters query, CancellationToken ct);
    Task<GameDto> GetByIdAsync(int id, CancellationToken ct);
    Task<GameDto> CreateAsync(CreateGameRequest request, CancellationToken ct);
    Task UpdateAsync(int id, UpdateGameRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}
