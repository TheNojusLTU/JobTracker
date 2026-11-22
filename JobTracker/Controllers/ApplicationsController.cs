using System.Security.Claims;
using JobTracker.Api;
using JobTracker.Filters;
using JobTracker.Models;
using JobTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace JobTracker.Controllers;

[ApiController]
[Authorize]
[Route("api/applications")]
[Tags("Applications")]
[ServiceFilter(typeof(ValidateCsrfTokenFilter))]
public sealed class ApplicationsController(
    IJobApplicationService applicationService,
    ICsvApplicationImportService importService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated user has no id claim.");

    [HttpGet("statistics", Name = "GetApplicationStatistics")]
    [EndpointSummary("Counts all of the current user's applications by status.")]
    [ProducesResponseType<ApplicationStatisticsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApplicationStatisticsResponse>> GetStatistics(CancellationToken cancellationToken)
    {
        return Ok(await applicationService.GetStatisticsAsync(UserId, cancellationToken));
    }

    [HttpGet(Name = "GetApplications")]
    [EndpointSummary("Lists the current user's applications, with optional search, filters and sorting.")]
    [ProducesResponseType<PagedResponse<ApplicationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetApplications(
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] ApplicationStatus? status = null,
        [FromQuery] string? location = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] bool newestFirst = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PagingExtensions.DefaultPageSize)
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
            return InvalidRequest(errors);
        }

        var result = await applicationService.GetApplicationsAsync(
            UserId, search, status, location, dates, newestFirst, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}", Name = "GetApplication")]
    [ProducesResponseType<ApplicationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetApplication(int id, CancellationToken cancellationToken)
    {
        var application = await applicationService.GetApplicationAsync(id, UserId, cancellationToken);
        return application is null ? NotFound() : Ok(application);
    }

    [HttpPost(Name = "CreateApplication")]
    [ProducesResponseType<ApplicationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateApplication(
        [FromBody] ApplicationRequest request, CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return InvalidRequest(errors);
        }

        var application = await applicationService.CreateApplicationAsync(request, UserId, cancellationToken);
        return Created($"/api/applications/{application.Id}", application);
    }

    [HttpPut("{id:int}", Name = "UpdateApplication")]
    [ProducesResponseType<ApplicationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateApplication(
        int id, [FromBody] ApplicationRequest request, CancellationToken cancellationToken)
    {
        var errors = request.Validate(requireStatus: true);
        if (errors.Count > 0)
        {
            return InvalidRequest(errors);
        }

        var application = await applicationService.UpdateApplicationAsync(id, request, UserId, cancellationToken);
        return application is null ? NotFound() : Ok(application);
    }

    [HttpDelete("{id:int}", Name = "DeleteApplication")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteApplication(int id, CancellationToken cancellationToken)
    {
        var deleted = await applicationService.DeleteApplicationAsync(id, UserId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("import", Name = "ImportApplications")]
    [EndpointSummary("Imports application rows from a CSV request stream.")]
    [ProducesResponseType<ApplicationImportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> ImportApplications(CancellationToken cancellationToken)
    {
        if (Request.ContentType?.StartsWith("text/csv", StringComparison.OrdinalIgnoreCase) != true)
        {
            return Problem(statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Expected a text/csv request body.");
        }

        var sizeFeature = HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false })
        {
            sizeFeature.MaxRequestBodySize = ICsvApplicationImportService.MaxImportBytes;
        }

        if (Request.ContentLength > ICsvApplicationImportService.MaxImportBytes)
        {
            return Problem(statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "CSV file is larger than 1 MB.");
        }

        var result = await importService.ImportAsync(Request.Body, UserId, cancellationToken);
        if (result.IsImportInProgress)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict,
                title: "A CSV import is already in progress.",
                detail: "Wait for the current import to finish, then try again.");
        }

        if (result.IsFileTooLarge)
        {
            return Problem(statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "CSV file is larger than 1 MB.");
        }

        return result.Errors is not null
            ? InvalidRequest(result.Errors)
            : Ok(new ApplicationImportResponse(result.ImportedCount!.Value));
    }

    private ActionResult InvalidRequest(IDictionary<string, string[]> errors)
    {
        var problem = ProblemDetailsFactory.CreateValidationProblemDetails(HttpContext, ModelState);
        foreach (var (key, messages) in errors)
        {
            problem.Errors[key] = messages;
        }

        return ValidationProblem(problem);
    }
}
