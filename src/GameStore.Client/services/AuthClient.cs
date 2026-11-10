using System.Net.Http.Json;
using GameStore.Shared.Dtos;

namespace GameStore.Client.Services;

public record ApiSettings(string BaseUrl);

public class AuthClient
{
    private readonly TokenStore _store;
    private readonly ClientAuthStateProvider _state;
    private readonly HttpClient _plainHttp;   // no auth handler: used for login/refresh/logout only

    // Refresh tokens rotate and the API revokes the whole session if one is reused,
    // so two parallel requests must never refresh at the same time.
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public AuthClient(TokenStore store, ClientAuthStateProvider state, ApiSettings settings)
    {
        _store = store;
        _state = state;
        _plainHttp = new HttpClient { BaseAddress = new Uri(settings.BaseUrl) };
    }

    public async Task LoginAsync(string email, string password)
    {
        try
        {
            using var response = await _plainHttp.PostAsJsonAsync("api/auth/login", new LoginRequest(email, password));
            await ApiErrors.EnsureSuccessAsync(response);

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>()
                       ?? throw new ApiException("The server returned an empty login response.");

            await _store.SetAsync(auth);
            _state.NotifyChanged();
        }
        catch (HttpRequestException)
        {
            throw new ApiException("Could not reach the server. Is the API running?");
        }
    }

    public async Task LogoutAsync()
    {
        var auth = await _store.GetAsync();

        if (auth is not null)
        {
            try
            {
                using var _ = await _plainHttp.PostAsJsonAsync("api/auth/logout", new LogoutRequest(auth.RefreshToken));
            }
            catch (HttpRequestException)
            {
                // Server unreachable: still sign out locally.
            }
        }

        await SignOutLocallyAsync();
    }

    /// <summary>Returns a usable access token, refreshing it first if it is about to expire.</summary>
    public async Task<string?> GetValidAccessTokenAsync()
    {
        var auth = await _store.GetAsync();
        if (auth is null) return null;
        if (!IsExpiring(auth)) return auth.AccessToken;

        await _refreshLock.WaitAsync();
        try
        {
            // Someone else may have refreshed while we were waiting.
            auth = await _store.GetAsync();
            if (auth is null) return null;
            if (!IsExpiring(auth)) return auth.AccessToken;

            if (auth.RefreshTokenExpiresAtUtc <= DateTime.UtcNow)
            {
                await SignOutLocallyAsync();
                return null;
            }

            try
            {
                using var response = await _plainHttp.PostAsJsonAsync(
                    "api/auth/refresh", new RefreshRequest(auth.RefreshToken));

                if (!response.IsSuccessStatusCode)
                {
                    await SignOutLocallyAsync();
                    return null;
                }

                var fresh = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (fresh is null)
                {
                    await SignOutLocallyAsync();
                    return null;
                }

                await _store.SetAsync(fresh);
                return fresh.AccessToken;
            }
            catch (HttpRequestException)
            {
                // Server unreachable: let the real request fail with its own error.
                return auth.AccessToken;
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static bool IsExpiring(AuthResponse auth)
    {
        try
        {
            return JwtParser.GetExpiry(auth.AccessToken) <= DateTimeOffset.UtcNow.AddSeconds(30);
        }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException)
        {
            return true;
        }
    }

    private async Task SignOutLocallyAsync()
    {
        await _store.ClearAsync();
        _state.NotifyChanged();
    }
}
