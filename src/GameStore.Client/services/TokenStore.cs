using System.Text.Json;
using GameStore.Shared.Dtos;
using Microsoft.JSInterop;

namespace GameStore.Client.Services;

/// <summary>
/// Keeps the login response in the browser's localStorage so a page refresh does
/// not log the admin out. (localStorage is readable by any script on the page, so
/// keep the app free of untrusted scripts / XSS.)
/// </summary>
public class TokenStore(IJSRuntime js)
{
    private const string Key = "gamestore.auth";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<AuthResponse?> GetAsync()
    {
        try
        {
            var raw = await js.InvokeAsync<string?>("localStorage.getItem", Key);
            return string.IsNullOrWhiteSpace(raw) ? null : JsonSerializer.Deserialize<AuthResponse>(raw, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SetAsync(AuthResponse auth) =>
        await js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(auth, Json));

    public async Task ClearAsync() =>
        await js.InvokeVoidAsync("localStorage.removeItem", Key);
}
