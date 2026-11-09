using System.Security.Claims;
using JobTracker.Services;
using Microsoft.AspNetCore.Antiforgery;

namespace JobTracker.Auth;

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

        var writes = auth.MapGroup("");
        writes.AddEndpointFilter(async (context, next) =>
        {
            var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
            if (!await antiforgery.IsRequestValidAsync(context.HttpContext))
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid CSRF token.",
                    detail: "Get /api/auth/csrf and send its request token in the X-CSRF-TOKEN header with the companion cookie.");
            }

            return await next(context);
        });

        writes.MapPost("/register", RegisterAsync).AllowAnonymous()
            .Produces<UserResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
        writes.MapPost("/login", LoginAsync).AllowAnonymous()
            .Produces<UserResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        writes.MapPost("/logout", async (AuthService authService) =>
        {
            await authService.LogoutAsync();
            return TypedResults.NoContent();
        }).RequireAuthorization()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapGet("/me", async (ClaimsPrincipal principal, AuthService authService) =>
        {
            var user = await authService.GetCurrentUserAsync(principal);
            return user is null
                ? Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Authentication required.")
                : Results.Ok(user);
        }).RequireAuthorization()
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> RegisterAsync(RegisterRequest request, AuthService authService)
    {
        var result = await authService.RegisterAsync(request);
        return result.Status switch
        {
            AuthStatus.Success => Results.Created("/api/auth/me", result.User),
            AuthStatus.DuplicateUsername => Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Username is already taken."),
            _ => Results.ValidationProblem(result.Errors!)
        };
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, AuthService authService)
    {
        var result = await authService.LoginAsync(request);
        return result.Status switch
        {
            AuthStatus.Success => Results.Ok(result.User),
            AuthStatus.InvalidCredentials => Results.Problem(statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid username or password."),
            _ => Results.ValidationProblem(result.Errors!)
        };
    }
}
