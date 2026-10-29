using System.Net.Mail;
using JobTracker.Contracts;
using JobTracker.Data;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JobTracker.Api;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth").WithTags("Authentication");
        auth.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });

        auth.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return TypedResults.Ok(new CsrfResponse(tokens.RequestToken!, tokens.HeaderName!));
        }).AllowAnonymous()
            .WithSummary("Gets a CSRF request token and sets its companion cookie.");

        var writes = auth.MapGroup("").AddEndpointFilter<CsrfEndpointFilter>();

        writes.MapPost("/register", RegisterAsync).AllowAnonymous()
            .Produces<UserResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
        writes.MapPost("/login", LoginAsync).AllowAnonymous()
            .Produces<UserResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        writes.MapPost("/logout", async (SignInManager<ApplicationUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return TypedResults.NoContent();
        }).RequireAuthorization()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapGet("/me", async (HttpContext context, UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.GetUserAsync(context.User);
            return user is null
                ? Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Authentication required.")
                : Results.Ok(UserResponse.FromUser(user));
        }).RequireAuthorization()
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
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
            return Results.ValidationProblem(errors);

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
            return DuplicateUsername();
        }

        if (!result.Succeeded)
        {
            if (result.Errors.Any(error => error.Code == "DuplicateUserName"))
                return DuplicateUsername();

            return Results.ValidationProblem(result.Errors
                .GroupBy(error => error.Code.StartsWith("Password", StringComparison.Ordinal) ? "password"
                    : error.Code.Contains("Email", StringComparison.Ordinal) ? "email" : "username")
                .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray()));
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return Results.Created("/api/auth/me", UserResponse.FromUser(user));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Username))
            errors["username"] = ["Username is required."];
        if (string.IsNullOrEmpty(request.Password))
            errors["password"] = ["Password is required."];
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var user = await userManager.FindByNameAsync(request.Username!.Trim());
        if (user is null || !(await signInManager.PasswordSignInAsync(
                user, request.Password!, isPersistent: false, lockoutOnFailure: true)).Succeeded)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid username or password.");
        }

        return Results.Ok(UserResponse.FromUser(user));
    }

    private static IResult DuplicateUsername() =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Username is already taken.");
}
