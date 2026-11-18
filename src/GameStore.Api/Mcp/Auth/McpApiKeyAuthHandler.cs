using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace GameStore.Api.Mcp.Auth;

/// <summary>
/// Authenticates "Authorization: Bearer gsmcp_..." for the /mcp endpoint only (it is not the
/// default scheme). Anything that does not look like an API key is ignored, so the normal JWT
/// handler deals with it.
/// </summary>
public sealed class McpApiKeyAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IMcpApiKeyService keys)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string BearerPrefix = "Bearer ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderNames.Authorization, out var values))
            return AuthenticateResult.NoResult();

        var header = values.ToString();

        if (!header.StartsWith(BearerPrefix + McpApiKeyDefaults.KeyPrefix, StringComparison.Ordinal))
            return AuthenticateResult.NoResult();

        var identity = await keys.ValidateAsync(header[BearerPrefix.Length..].Trim(), Context.RequestAborted);

        if (identity is null)
            return AuthenticateResult.Fail("Invalid, expired or revoked API key.");

        var claims = new List<Claim>
        {
            new("sub", identity.UserId),
            new(ClaimTypes.NameIdentifier, identity.UserId),
            new(ClaimTypes.Name, identity.UserName),
            new(McpApiKeyDefaults.KeyIdClaim, identity.KeyId.ToString()),
            new(McpApiKeyDefaults.ReadOnlyClaim, identity.ReadOnly ? "true" : "false")
        };

        claims.AddRange(identity.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));

        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}