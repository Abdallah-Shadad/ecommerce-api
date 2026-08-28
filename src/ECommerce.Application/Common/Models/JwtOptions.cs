namespace ECommerce.Application.Common.Models;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int DurationInMinutes { get; init; }
    public int RefreshTokenExpiryInDays { get; init; }
}