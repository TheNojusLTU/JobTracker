using JobTracker.Contracts;

namespace JobTracker.Services;

public sealed record AuthResult(AuthStatus Status, UserResponse? User = null, Dictionary<string, string[]>? Errors = null)
{
    public static AuthResult Success(UserResponse user) => new(AuthStatus.Success, User: user);

    public static AuthResult Invalid(Dictionary<string, string[]> errors) => new(AuthStatus.Invalid, Errors: errors);

    public static AuthResult DuplicateUsername() => new(AuthStatus.DuplicateUsername);

    public static AuthResult InvalidCredentials() => new(AuthStatus.InvalidCredentials);
}
