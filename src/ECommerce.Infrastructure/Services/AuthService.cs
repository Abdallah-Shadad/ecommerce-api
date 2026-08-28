using ECommerce.Application.DTOs.Auth;
using ECommerce.Application.Interfaces.Services;
using ECommerce.Domain.Entities.Cart;
using ECommerce.Domain.Entities.Identity;
using ECommerce.Domain.Exceptions;
using ECommerce.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly AppDbContext _dbContext;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IJwtTokenGenerator jwtTokenGenerator,
        AppDbContext dbContext)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _dbContext = dbContext;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var existingEmail = await _userManager.FindByEmailAsync(request.Email);
        if (existingEmail != null)
            throw new ConflictException("Email is already registered.");

        var existingUserName = await _userManager.FindByNameAsync(request.UserName);
        if (existingUserName != null)
            throw new ConflictException("Username is already taken.");

        var user = new ApplicationUser
        {
            UserName = request.UserName,
            Email = request.Email,
            FullName = request.FullName,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new ConflictException($"User registration failed: {errors}");
        }

        if (await _roleManager.RoleExistsAsync("Customer"))
        {
            await _userManager.AddToRoleAsync(user, "Customer");
        }

        var cart = new Cart { UserId = user.Id };
        _dbContext.Set<Cart>().Add(cart);
        await _dbContext.SaveChangesAsync();

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            throw new UnauthorizedException("Invalid email or password.");

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw new UnauthorizedException("Refresh token is required.");

        var existingToken = await _dbContext.Set<RefreshToken>()
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == request.RefreshToken);

        if (existingToken == null)
            throw new UnauthorizedException("Invalid refresh token.");

        if (existingToken.RevokedAtUtc != null)
        {
            var gracePeriod = TimeSpan.FromSeconds(15);
            var isWithinGracePeriod = DateTime.UtcNow - existingToken.RevokedAtUtc <= gracePeriod;

            if (isWithinGracePeriod && !string.IsNullOrEmpty(existingToken.ReplacedByToken))
            {
                var replacementToken = await _dbContext.Set<RefreshToken>()
                    .FirstOrDefaultAsync(r => r.Token == existingToken.ReplacedByToken);

                if (replacementToken != null && replacementToken.IsActive)
                {
                    var userRoles = await _userManager.GetRolesAsync(existingToken.User!);
                    var newAccessToken = _jwtTokenGenerator.GenerateAccessToken(existingToken.User!, userRoles);

                    return new AuthResponse(
                        newAccessToken,
                        replacementToken.Token,
                        replacementToken.ExpiresAtUtc,
                        existingToken.User!.FullName,
                        existingToken.User!.Email!,
                        userRoles.ToList()
                    );
                }
            }

            var activeTokens = await _dbContext.Set<RefreshToken>()
                .Where(r => r.UserId == existingToken.UserId && r.RevokedAtUtc == null)
                .ToListAsync();

            foreach (var token in activeTokens)
            {
                token.RevokedAtUtc = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
            throw new UnauthorizedException("Compromised refresh token detected. All active sessions have been revoked.");
        }

        if (existingToken.ExpiresAtUtc <= DateTime.UtcNow)
            throw new UnauthorizedException("Expired refresh token.");

        var user = existingToken.User;
        if (user == null)
            throw new UnauthorizedException("Associated user not found.");

        var newRefreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        newRefreshToken.UserId = user.Id;

        existingToken.RevokedAtUtc = DateTime.UtcNow;
        existingToken.ReplacedByToken = newRefreshToken.Token;

        _dbContext.Set<RefreshToken>().Add(newRefreshToken);
        await _dbContext.SaveChangesAsync();

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user, roles);

        return new AuthResponse(
            accessToken,
            newRefreshToken.Token,
            newRefreshToken.ExpiresAtUtc,
            user.FullName,
            user.Email!,
            roles.ToList()
        );
    }

    public async Task<bool> RevokeTokenAsync(RevokeTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return false;

        var tokenEntity = await _dbContext.Set<RefreshToken>()
            .FirstOrDefaultAsync(r => r.Token == request.Token);

        if (tokenEntity == null || !tokenEntity.IsActive)
            return false;

        tokenEntity.RevokedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return true;
    }

    private async Task<AuthResponse> GenerateAuthResponseAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user, roles);

        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        refreshToken.UserId = user.Id;

        _dbContext.Set<RefreshToken>().Add(refreshToken);
        await _dbContext.SaveChangesAsync();

        return new AuthResponse(
            accessToken,
            refreshToken.Token,
            refreshToken.ExpiresAtUtc,
            user.FullName,
            user.Email!,
            roles.ToList()
        );
    }
}