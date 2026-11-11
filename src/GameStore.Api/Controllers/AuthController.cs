using GameStore.Shared.Dtos;
using GameStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameStore.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    private const string RefreshCookieName = "refresh_token";

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var response = await authService.RegisterAsync(request, ct);
        SetRefreshCookie(response.RefreshToken, response.RefreshTokenExpiresAtUtc);

        return StatusCode(StatusCodes.Status201Created, MaskRefreshToken(response));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var response = await authService.LoginAsync(request, ct);
        SetRefreshCookie(response.RefreshToken, response.RefreshTokenExpiresAtUtc);

        return Ok(MaskRefreshToken(response));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized();
        }

        var response = await authService.RefreshAsync(new RefreshRequest(refreshToken), ct);
        SetRefreshCookie(response.RefreshToken, response.RefreshTokenExpiresAtUtc);

        return Ok(MaskRefreshToken(response));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<ActionResult> Logout(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await authService.LogoutAsync(new LogoutRequest(refreshToken), ct);
        }

        RemoveRefreshCookie();
        return Ok();
    }

    private void SetRefreshCookie(string refreshToken, DateTime expiresAtUtc)
    {
        Response.Cookies.Append(RefreshCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = expiresAtUtc,
            IsEssential = true,
            Path = "/"
        });
    }

    private void RemoveRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });
    }

    private static AuthResponse MaskRefreshToken(AuthResponse response) =>
        response with { RefreshToken = string.Empty };
}