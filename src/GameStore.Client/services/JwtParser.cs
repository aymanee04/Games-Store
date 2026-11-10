using System.Security.Claims;
using System.Text.Json;

namespace GameStore.Client.Services;

/// <summary>
/// Reads the claims out of the access token so the UI can show the user name and
/// hide admin-only links. This is for display only: the API still checks the role.
/// </summary>
public static class JwtParser
{
    public static IReadOnlyList<Claim> ParseClaims(string jwt)
    {
        using var doc = JsonDocument.Parse(DecodePayload(jwt));
        var claims = new List<Claim>();

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            var type = MapClaimType(property.Name);

            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.Value.EnumerateArray())
                    claims.Add(new Claim(type, item.ToString()));
            }
            else
            {
                claims.Add(new Claim(type, property.Value.ToString()));
            }
        }

        return claims;
    }

    public static DateTimeOffset GetExpiry(string jwt)
    {
        using var doc = JsonDocument.Parse(DecodePayload(jwt));

        return doc.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : DateTimeOffset.MinValue;
    }

    // The API writes the role as the long ClaimTypes.Role URI; other libraries use "role".
    private static string MapClaimType(string name) => name switch
    {
        "role" or "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" => ClaimTypes.Role,
        "unique_name" or "name" => ClaimTypes.Name,
        "email" => ClaimTypes.Email,
        "sub" => ClaimTypes.NameIdentifier,
        _ => name
    };

    private static byte[] DecodePayload(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3) throw new FormatException("Invalid JWT.");

        var base64 = parts[1].Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Convert.FromBase64String(base64);
    }
}
