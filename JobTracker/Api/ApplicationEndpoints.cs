using System.Security.Claims;
using JobTracker.Models;
using JobTracker.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.HttpResults;

namespace JobTracker.Api;

public static class ApplicationEndpoints
{
    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/applications")
            .RequireAuthorization()
            .WithTags("Applications");

        group.MapGet("/", GetApplications)
            .WithName("GetApplications")
            .WithSummary("Lists the current user's applications, with optional search, filters and sorting.");
        group.MapGet("/{id:int}", GetApplication)
            .WithName("GetApplication");

        var writes = group.MapGroup("");
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

        writes.MapPost("/", CreateApplication)
            .WithName("CreateApplication");
        writes.MapPut("/{id:int}", UpdateApplication)
            .WithName("UpdateApplication");
        writes.MapDelete("/{id:int}", DeleteApplication)
            .WithName("DeleteApplication");
        writes.MapPost("/import", ImportApplications)
            .WithName("ImportApplications")
            .WithSummary("Imports application rows from a CSV request stream.");

        return app;
    }

    private static async Task<IResult> ImportApplications(
        HttpContext context,
        ClaimsPrincipal user,
        CsvApplicationImportService importService,
        CancellationToken cancellationToken)
    {
        if (context.Request.ContentType?.StartsWith("text/csv", StringComparison.OrdinalIgnoreCase) != true)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Expected a text/csv request body.");
        }

        var sizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false })
        {
            sizeFeature.MaxRequestBodySize = CsvApplicationImportService.MaxImportBytes;
        }

        if (context.Request.ContentLength > CsvApplicationImportService.MaxImportBytes)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "CSV file is larger than 1 MB.");
        }

        var result = await importService.ImportAsync(
            context.Request.Body,
            GetUserId(user),
            cancellationToken);

        if (result.IsImportInProgress)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "A CSV import is already in progress.",
                detail: "Wait for the current import to finish, then try again.");
        }

        if (result.IsFileTooLarge)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "CSV file is larger than 1 MB.");
        }

        return result.Errors is not null
            ? Results.ValidationProblem(result.Errors)
            : Results.Ok(new ApplicationImportResponse(result.ImportedCount!.Value));
    }

    public static async Task<Results<Ok<PagedResponse<ApplicationResponse>>, ValidationProblem>> GetApplications(
        ClaimsPrincipal user,
        JobApplicationService service,
        CancellationToken cancellationToken,
        string? search = null,
        ApplicationStatus? status = null,
        string? location = null,
        DateTime? from = null,
        DateTime? to = null,
        bool newestFirst = true,
        int page = 1,
        int pageSize = PagingExtensions.DefaultPageSize)
    {
        var errors = PagingExtensions.ValidatePaging(page, pageSize);
        DateRange? dates = null;
        try
        {
            dates = from is null && to is null ? null : new DateRange(from, to);
        }
        catch (InvalidDateRangeException exception)
        {
            errors["from"] = [exception.Message];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var result = await service.GetApplicationsAsync(
            GetUserId(user),
            search,
            status,
            location,
            dates,
            newestFirst,
            page,
            pageSize,
            cancellationToken);

        return TypedResults.Ok(result);
    }

    public static async Task<Results<Ok<ApplicationResponse>, NotFound>> GetApplication(
        int id,
        ClaimsPrincipal user,
        JobApplicationService service,
        CancellationToken cancellationToken)
    {
        var application = await service.GetApplicationAsync(
            id, GetUserId(user), cancellationToken);

        return application is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(application);
    }

    public static async Task<Results<Created<ApplicationResponse>, ValidationProblem>> CreateApplication(
        ApplicationRequest request,
        ClaimsPrincipal user,
        JobApplicationService service,
        CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var application = await service.CreateApplicationAsync(
            request, GetUserId(user), cancellationToken);

        return TypedResults.Created($"/api/applications/{application.Id}", application);
    }

    public static async Task<Results<Ok<ApplicationResponse>, NotFound, ValidationProblem>> UpdateApplication(
        int id,
        ApplicationRequest request,
        ClaimsPrincipal user,
        JobApplicationService service,
        CancellationToken cancellationToken)
    {
        var errors = request.Validate(requireStatus: true);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var application = await service.UpdateApplicationAsync(
            id, request, GetUserId(user), cancellationToken);

        return application is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(application);
    }

    public static async Task<Results<NoContent, NotFound>> DeleteApplication(
        int id,
        ClaimsPrincipal user,
        JobApplicationService service,
        CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteApplicationAsync(
            id, GetUserId(user), cancellationToken);

        return deleted
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
    }

    private static string GetUserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated user has no id claim.");
}
