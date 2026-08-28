using ECommerce.Domain.Entities.Identity;

namespace ECommerce.Application.Interfaces.Services;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles);
    RefreshToken GenerateRefreshToken();
}