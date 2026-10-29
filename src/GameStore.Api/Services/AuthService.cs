using GameStore.Api.Data;
using GameStore.Api.Dtos;
using GameStore.Api.Entities;
using GameStore.Api.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GameStore.Api.Services;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ITokenService tokenService,
    AppDbContext db,
    TimeProvider timeProvider,
    IConfiguration configuration) : IAuthService
{
    private const int RefreshTokenDays = 7;

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var user = new ApplicationUser
        {
            UserName = request.UserName,
            Email = request.Email
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail"))
            {
                throw new ConflictException("A user with this email or username already exists.");
            }

            throw new BadRequestException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        var roleResult = await userManager.AddToRoleAsync(user, "User");

        if (!roleResult.Succeeded)
        {
            throw new BadRequestException(string.Join(" ", roleResult.Errors.Select(e => e.Description)));
        }
        

        var now = UtcNow();
        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokenService.CreateAccessToken(user, roles);

        var rawRefreshToken = tokenService.GenerateRefreshToken();
        var refreshTokenHash = tokenService.HashToken(rawRefreshToken);
        var familyId = Guid.NewGuid();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            FamilyId = familyId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(RefreshTokenDays)
        });

        await db.SaveChangesAsync(ct);

        return new AuthResponse(
            AccessToken: accessToken,
            AccessTokenExpiresAtUtc: now.AddMinutes(GetAccessTokenMinutes()),
            RefreshToken: rawRefreshToken,
            RefreshTokenExpiresAtUtc: now.AddDays(RefreshTokenDays));
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            throw new UnauthorizedException("Invalid credentials.");
        }

        var check = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);

        if (!check.Succeeded)
        {
            throw new UnauthorizedException(
                check.IsLockedOut ? "Account temporarily locked." : "Invalid credentials.");
        }

        var now = UtcNow();
        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokenService.CreateAccessToken(user, roles);

        var rawRefreshToken = tokenService.GenerateRefreshToken();
        var refreshTokenHash = tokenService.HashToken(rawRefreshToken);
        var familyId = Guid.NewGuid();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            FamilyId = familyId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(RefreshTokenDays)
        });

        await db.SaveChangesAsync(ct);

        return new AuthResponse(
            AccessToken: accessToken,
            AccessTokenExpiresAtUtc: now.AddMinutes(GetAccessTokenMinutes()),
            RefreshToken: rawRefreshToken,
            RefreshTokenExpiresAtUtc: now.AddDays(RefreshTokenDays));
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        var now = UtcNow();
        var incomingHash = tokenService.HashToken(request.RefreshToken);

        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == incomingHash, ct);

        if (stored is null)
        {
            throw new UnauthorizedException("Invalid refresh token.");
        }

        if (stored.RevokedAtUtc is not null)
        {
            await RevokeFamilyAsync(stored.FamilyId, now, ct);
            throw new UnauthorizedException("Invalid refresh token.");
        }

        if (stored.ExpiresAtUtc <= now)
        {
            throw new UnauthorizedException("Refresh token expired.");
        }

        if (await userManager.IsLockedOutAsync(stored.User))
        {
            await RevokeFamilyAsync(stored.FamilyId, now, ct);
            throw new UnauthorizedException("Account temporarily locked.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Atomic claim: only one concurrent request can flip null -> now.
            var claimed = await db.RefreshTokens
                .Where(t => t.Id == stored.Id && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);

            if (claimed == 0)
            {
                await RevokeFamilyAsync(stored.FamilyId, now, ct);
                await tx.CommitAsync(ct); // persist family revocation
                throw new UnauthorizedException("Invalid refresh token.");
            }

            var roles = await userManager.GetRolesAsync(stored.User);
            var accessToken = tokenService.CreateAccessToken(stored.User, roles);

            var newRawRefreshToken = tokenService.GenerateRefreshToken();
            var newRefreshTokenHash = tokenService.HashToken(newRawRefreshToken);

            // Link old -> new within same transaction.
            await db.RefreshTokens
                .Where(t => t.Id == stored.Id)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(t => t.ReplacedByTokenHash, newRefreshTokenHash),
                    ct);

            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = stored.UserId,
                TokenHash = newRefreshTokenHash,
                FamilyId = stored.FamilyId,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddDays(RefreshTokenDays)
            });

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return new AuthResponse(
                AccessToken: accessToken,
                AccessTokenExpiresAtUtc: now.AddMinutes(GetAccessTokenMinutes()),
                RefreshToken: newRawRefreshToken,
                RefreshTokenExpiresAtUtc: now.AddDays(RefreshTokenDays));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken ct)
    {
        var now = UtcNow();
        var incomingHash = tokenService.HashToken(request.RefreshToken);

        var stored = await db.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.TokenHash == incomingHash, ct);

        if (stored is null)
        {
            // Idempotent success, no token existence leak.
            return;
        }

        await RevokeFamilyAsync(stored.FamilyId, now, ct);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken ct)
    {
        await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private int GetAccessTokenMinutes()
        => configuration.GetValue<int?>("Jwt:AccessTokenMinutes") ?? 15;
}