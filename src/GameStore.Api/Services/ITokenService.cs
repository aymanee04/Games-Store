using GameStore.Api.Entities;

namespace GameStore.Api.Services;

public interface ITokenService
{
    string CreateAccessToken(ApplicationUser user, IList<string> roles);
    string GenerateRefreshToken();
    string HashToken(string token);
}