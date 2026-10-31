using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GameStore.Api.Entities;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;

namespace GameStore.Api.Services;

public class TokenService(IConfiguration configuration) : ITokenService
{
    public string CreateAccessToken(ApplicationUser user, IList<string> roles)
    {
        var jwtKey = configuration["JWT_KEY"]
                     ?? throw new InvalidOperationException("JWT_KEY configuration is missing.");

        var issuer = configuration["Jwt:Issuer"]
                     ?? "GameStore.Api";

        var audience = configuration["Jwt:Audience"]
                       ?? "GameStore.Api";
        
        var accessTokenMinutes = configuration.GetValue<int?>("Jwt:AccessTokenMinutes") ?? 15;

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(accessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    
    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    public string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }
}