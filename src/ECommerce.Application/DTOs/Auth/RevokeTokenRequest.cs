namespace ECommerce.Application.DTOs.Auth;

public record RevokeTokenRequest(
    string? Token = null
);