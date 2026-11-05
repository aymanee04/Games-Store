using System.ComponentModel.DataAnnotations;
using GameStore.Api.validations;

namespace GameStore.Api.Dtos;

public record RegisterRequest(
    [Required, StringLength(50, MinimumLength = 3), NoWhitespace] string UserName,
    [Required, EmailAddress, NoWhitespace] string Email,
    [Required, NoWhitespace, MinLength(6), MaxLength(50)] string Password);

public record LoginRequest(
    [Required, EmailAddress, NoWhitespace] string Email,
    [Required, NoWhitespace, MinLength(6), MaxLength(50)] string Password);

public record AuthResponse(
    string AccessToken, DateTime AccessTokenExpiresAtUtc,
    string RefreshToken, DateTime RefreshTokenExpiresAtUtc);

public record RefreshRequest([Required] string RefreshToken);
public record LogoutRequest([Required] string RefreshToken);