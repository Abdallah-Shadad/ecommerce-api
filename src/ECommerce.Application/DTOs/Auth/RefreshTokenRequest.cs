namespace ECommerce.Application.DTOs.Auth;

public record RefreshTokenRequest(
    string? RefreshToken = null,
    string? ExpiredAccessToken = null
);