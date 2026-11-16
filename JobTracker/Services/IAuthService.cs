using System.Security.Claims;
using JobTracker.Auth;

namespace JobTracker.Services;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request);

    Task<AuthResult> LoginAsync(LoginRequest request);

    Task LogoutAsync();

    Task<UserResponse?> GetCurrentUserAsync(ClaimsPrincipal principal);
}
