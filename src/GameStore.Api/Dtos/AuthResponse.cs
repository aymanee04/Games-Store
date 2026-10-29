using System.ComponentModel.DataAnnotations;

namespace GameStore.Api.Dtos;

public record RegisterRequest(
    [Required, StringLength(50, MinimumLength = 3)] string UserName,
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record LoginRequest(
    [Required] string Email,
    [Required] string Password);

public record AuthResponse(
    string AccessToken, DateTime AccessTokenExpiresAtUtc,
    string RefreshToken, DateTime RefreshTokenExpiresAtUtc);

public record RefreshRequest([Required] string RefreshToken);
public record LogoutRequest([Required] string RefreshToken);