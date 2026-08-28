namespace ECommerce.Application.DTOs.Auth;

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime RefreshTokenExpiresOn,
    string FullName,
    string Email,
    IReadOnlyList<string> Roles
);