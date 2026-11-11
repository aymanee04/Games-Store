using System.Text.Json;
using GameStore.Shared.Dtos;
using Microsoft.JSInterop;

namespace GameStore.Client.Services;

/// <summary>
/// Keeps the access token in browser storage, while the refresh token stays in an HttpOnly cookie.
/// </summary>
public class TokenStore(IJSRuntime js)
{
    private const string Key = "gamestore.access";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<AuthResponse?> GetAsync()
    {
        try
        {
            var raw = await js.InvokeAsync<string?>("localStorage.getItem", Key);
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var accessToken = JsonSerializer.Deserialize<string>(raw, Json);
            if (string.IsNullOrWhiteSpace(accessToken)) return null;

            return new AuthResponse(
                AccessToken: accessToken,
                AccessTokenExpiresAtUtc: DateTime.MinValue,
                RefreshToken: string.Empty,
                RefreshTokenExpiresAtUtc: DateTime.MinValue);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SetAsync(AuthResponse auth) =>
        await js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(auth.AccessToken, Json));

    public async Task ClearAsync() =>
        await js.InvokeVoidAsync("localStorage.removeItem", Key);
}
