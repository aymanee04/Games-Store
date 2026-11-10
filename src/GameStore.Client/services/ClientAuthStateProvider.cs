using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace GameStore.Client.Services;

public class ClientAuthStateProvider(TokenStore store) : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var auth = await store.GetAsync();
        if (auth is null) return Anonymous;

        // An expired access token is fine while the refresh token is still valid:
        // AuthTokenHandler renews it on the next API call.
        if (auth.RefreshTokenExpiresAtUtc <= DateTime.UtcNow) return Anonymous;

        try
        {
            var identity = new ClaimsIdentity(
                JwtParser.ParseClaims(auth.AccessToken), "jwt", ClaimTypes.Name, ClaimTypes.Role);

            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException)
        {
            return Anonymous;
        }
    }

    public void NotifyChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
