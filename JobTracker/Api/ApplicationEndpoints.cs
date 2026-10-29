using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using JobTracker.Contracts;
using JobTracker.Extensions;
using JobTracker.Models;
using JobTracker.Services;
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

        // same CSRF check as the auth POSTs, only for requests that change data
        var writes = group.MapGroup("").AddEndpointFilter<CsrfEndpointFilter>();

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
        IApplicationImportService importService)
    {
        var request = context.Request;
        if (request.ContentType?.StartsWith("text/csv", StringComparison.OrdinalIgnoreCase) != true)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Expected a text/csv request body.");
        }

        var sizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false })
        {
            sizeFeature.MaxRequestBodySize = ApplicationImportService.MaxImportBytes;
        }

        var result = request.ContentLength > ApplicationImportService.MaxImportBytes
            ? ApplicationImportResult.FileTooLarge()
            : await importService.ImportAsync(user.GetUserId(), request.Body, cancellationToken: context.RequestAborted);

        if (result.TooLarge)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "CSV file is larger than 1 MB.");
        }

        return result.Errors.Count > 0
            ? Results.ValidationProblem(result.Errors)
            : Results.Ok(new ApplicationImportResponse(result.ImportedCount));
    }

    public static async Task<Results<Ok<PagedResponse<ApplicationResponse>>, ValidationProblem>> GetApplications(
        ClaimsPrincipal user,
        IApplicationService applications,
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
        if (from is not null && to is not null && from.Value.Date > to.Value.Date)
        {
            errors["from"] = ["From date can't be after To date."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var result = await applications.ListAsync(
            user.GetUserId(),
            search: search,
            status: status,
            location: location,
            dates: from is null && to is null ? null : new DateRange(from, to),
            newestFirst: newestFirst,
            page: page,
            pageSize: pageSize);

        return TypedResults.Ok(result);
    }

    public static async Task<Results<Ok<ApplicationResponse>, NotFound>> GetApplication(
        int id,
        ClaimsPrincipal user,
        IApplicationService applications)
    {
        var application = await applications.GetAsync(user.GetUserId(), id);

        return application is null ? TypedResults.NotFound() : TypedResults.Ok(application);
    }

    public static async Task<Results<Created<ApplicationResponse>, ValidationProblem>> CreateApplication(
        ApplicationRequest request,
        ClaimsPrincipal user,
        IApplicationService applications)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var application = await applications.CreateAsync(user.GetUserId(), request);

        return TypedResults.Created($"/api/applications/{application.Id}", application);
    }

    public static async Task<Results<Ok<ApplicationResponse>, NotFound, ValidationProblem>> UpdateApplication(
        int id,
        ApplicationRequest request,
        ClaimsPrincipal user,
        IApplicationService applications)
    {
        var errors = request.Validate(requireStatus: true);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var application = await applications.UpdateAsync(user.GetUserId(), id, request);

        return application is null ? TypedResults.NotFound() : TypedResults.Ok(application);
    }

    public static async Task<Results<NoContent, NotFound>> DeleteApplication(
        int id,
        ClaimsPrincipal user,
        IApplicationService applications)
    {
        var deleted = await applications.DeleteAsync(user.GetUserId(), id);

        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
