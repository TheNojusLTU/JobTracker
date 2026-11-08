using Microsoft.AspNetCore.Antiforgery;

namespace JobTracker.Api;

public class CsrfEndpointFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!await antiforgery.IsRequestValidAsync(context.HttpContext))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid CSRF token.",
                detail: "Get /api/auth/csrf and send its request token in the X-CSRF-TOKEN header with the companion cookie.");
        }

        return await next(context);
    }
}
