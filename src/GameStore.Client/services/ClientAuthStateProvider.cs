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
        if (auth is null || string.IsNullOrWhiteSpace(auth.AccessToken)) return Anonymous;

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
