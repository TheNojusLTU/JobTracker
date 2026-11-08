using System.Net.Mail;
using System.Security.Claims;
using JobTracker.Contracts;
using JobTracker.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JobTracker.Services;

public class AuthService(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager) : IAuthService
{
    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        var username = request.Username?.Trim() ?? "";
        var displayName = request.DisplayName?.Trim() ?? "";
        var email = request.Email?.Trim();
        var avatarKey = string.IsNullOrEmpty(request.AvatarKey) ? AvatarCatalog.Default.Key : request.AvatarKey;
        var errors = new Dictionary<string, string[]>();

        if (username.Length is < 3 or > 20)
            errors["username"] = ["Username must be between 3 and 20 characters."];
        if (displayName.Length is < 3 or > 20)
            errors["displayName"] = ["Display name must be between 3 and 20 characters."];
        if (!string.IsNullOrEmpty(email) && !MailAddress.TryCreate(email, out _))
            errors["email"] = ["Enter a valid email address, or leave it blank."];
        if (string.IsNullOrEmpty(request.Password))
            errors["password"] = ["Password is required."];
        if (!AvatarCatalog.Contains(avatarKey))
            errors["avatarKey"] = ["Choose an available avatar."];
        if (errors.Count > 0)
            return AuthResult.Invalid(errors);

        var user = new ApplicationUser
        {
            UserName = username,
            DisplayName = displayName,
            Email = string.IsNullOrEmpty(email) ? null : email,
            AvatarKey = avatarKey
        };

        IdentityResult result;
        try
        {
            result = await userManager.CreateAsync(user, request.Password!);
        }
        // The unique index also handles simultaneous registrations of the same normalized username.
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "UserNameIndex" })
        {
            return AuthResult.DuplicateUsername();
        }

        if (!result.Succeeded)
        {
            if (result.Errors.Any(error => error.Code == "DuplicateUserName"))
                return AuthResult.DuplicateUsername();

            return AuthResult.Invalid(result.Errors
                .GroupBy(error => error.Code.StartsWith("Password", StringComparison.Ordinal) ? "password"
                    : error.Code.Contains("Email", StringComparison.Ordinal) ? "email" : "username")
                .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray()));
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return AuthResult.Success(UserResponse.FromUser(user));
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Username))
            errors["username"] = ["Username is required."];
        if (string.IsNullOrEmpty(request.Password))
            errors["password"] = ["Password is required."];
        if (errors.Count > 0)
            return AuthResult.Invalid(errors);

        var user = await userManager.FindByNameAsync(request.Username!.Trim());
        if (user is null || !(await signInManager.PasswordSignInAsync(
                user, request.Password!, isPersistent: false, lockoutOnFailure: true)).Succeeded)
        {
            return AuthResult.InvalidCredentials();
        }

        return AuthResult.Success(UserResponse.FromUser(user));
    }

    public Task LogoutAsync() => signInManager.SignOutAsync();

    public async Task<UserResponse?> GetCurrentUserAsync(ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);
        return user is null ? null : UserResponse.FromUser(user);
    }
}
