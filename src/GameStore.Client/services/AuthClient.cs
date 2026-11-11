using System.Net.Http.Json;
using GameStore.Shared.Dtos;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace GameStore.Client.Services;

public record ApiSettings(string BaseUrl);

public class AuthClient
{
    private readonly TokenStore _store;
    private readonly ClientAuthStateProvider _state;
    private readonly HttpClient _plainHttp;
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
            using var response = await PostWithCookiesAsync("api/auth/login", new LoginRequest(email, password));
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
        try
        {
            using var _ = await PostWithCookiesAsync("api/auth/logout", null);
        }
        catch (HttpRequestException)
        {
            // Server unreachable: still sign out locally.
        }

        await SignOutLocallyAsync();
    }

    public async Task<bool> TryRefreshOnStartupAsync()
    {
        var auth = await _store.GetAsync();
        if (auth is null || string.IsNullOrWhiteSpace(auth.AccessToken))
            return false;

        if (!IsExpiring(auth))
            return true;

        await _refreshLock.WaitAsync();
        try
        {
            auth = await _store.GetAsync();
            if (auth is null || string.IsNullOrWhiteSpace(auth.AccessToken))
                return false;

            if (!IsExpiring(auth))
                return true;

            using var response = await PostWithCookiesAsync("api/auth/refresh", null);
            if (!response.IsSuccessStatusCode)
            {
                await SignOutLocallyAsync();
                return false;
            }

            var fresh = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (fresh is null)
            {
                await SignOutLocallyAsync();
                return false;
            }

            await _store.SetAsync(fresh);
            _state.NotifyChanged();
            return true;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task<string?> GetValidAccessTokenAsync()
    {
        var auth = await _store.GetAsync();
        if (auth is null || string.IsNullOrWhiteSpace(auth.AccessToken)) return null;
        if (!IsExpiring(auth)) return auth.AccessToken;

        await _refreshLock.WaitAsync();
        try
        {
            auth = await _store.GetAsync();
            if (auth is null || string.IsNullOrWhiteSpace(auth.AccessToken)) return null;
            if (!IsExpiring(auth)) return auth.AccessToken;

            try
            {
                using var response = await PostWithCookiesAsync("api/auth/refresh", null);
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
                _state.NotifyChanged();
                return fresh.AccessToken;
            }
            catch (HttpRequestException)
            {
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
    
    private Task<HttpResponseMessage> PostWithCookiesAsync(string url, object? body = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        if (body is not null) req.Content = JsonContent.Create(body);
        req.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return _plainHttp.SendAsync(req);
    }
}