using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace JobTracker.Filters;

public sealed class ValidateCsrfTokenFilter(
    IAntiforgery antiforgery,
    ProblemDetailsFactory problemDetailsFactory) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) ||
            HttpMethods.IsOptions(request.Method) || HttpMethods.IsTrace(request.Method))
        {
            return;
        }

        if (!await antiforgery.IsRequestValidAsync(context.HttpContext))
        {
            var problem = problemDetailsFactory.CreateProblemDetails(
                context.HttpContext,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid CSRF token.",
                detail: "Get /api/auth/csrf and send its request token in the X-CSRF-TOKEN header with the companion cookie.");
            context.Result = new BadRequestObjectResult(problem);
        }
    }
}
