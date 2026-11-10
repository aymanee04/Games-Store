using System.Net.Http.Json;
using GameStore.Shared.Dtos;

namespace GameStore.Client.Services;

/// <summary>Public, read-only calls: anyone can browse games, genres and studios.</summary>
public class StoreApi(HttpClient http)
{
    public Task<List<GenreDto>> GetGenresAsync(CancellationToken ct = default) =>
        GetAsync<List<GenreDto>>("api/genres", ct);

    public Task<List<StudioDtos>> GetStudiosAsync(CancellationToken ct = default) =>
        GetAsync<List<StudioDtos>>("api/studios", ct);

    public Task<PagedResult<GameDto>> GetGamesAsync(
        int? genreId, int? studioId, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var url = $"api/games?page={page}&pageSize={pageSize}";

        if (genreId is not null) url += $"&genreId={genreId}";
        if (studioId is not null) url += $"&studioId={studioId}";
        if (!string.IsNullOrWhiteSpace(search)) url += $"&search={Uri.EscapeDataString(search.Trim())}";

        return GetAsync<PagedResult<GameDto>>(url, ct);
    }

    private async Task<T> GetAsync<T>(string url, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, ct);
        await ApiErrors.EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
               ?? throw new ApiException("The server returned an empty response.");
    }
}
