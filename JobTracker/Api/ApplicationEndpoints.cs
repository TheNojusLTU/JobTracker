using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.VisualBasic.FileIO;
using JobTracker.Contracts;
using JobTracker.Data;
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

    private const long MaxImportBytes = 1_048_576;
    private const int MaxImportRows = 500;
    private static readonly string[] ImportHeaders =
    [
        "CompanyName", "JobTitle", "Location", "Salary",
        "JobUrl", "ApplicationDate", "Status", "Notes"
    ];

    private static async Task<IResult> ImportApplications(
        HttpContext context,
        ClaimsPrincipal user,
        ApplicationDbContext dbContext)
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
            sizeFeature.MaxRequestBodySize = MaxImportBytes;
        }

        if (request.ContentLength > MaxImportBytes)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "CSV file is larger than 1 MB.");
        }

        using var csvStream = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var remainingBytes = (int)(MaxImportBytes - csvStream.Length + 1);
            var bytesRead = await request.Body.ReadAsync(
                buffer.AsMemory(0, Math.Min(buffer.Length, remainingBytes)),
                context.RequestAborted);
            if (bytesRead == 0)
            {
                break;
            }

            if (csvStream.Length + bytesRead > MaxImportBytes)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status413PayloadTooLarge,
                    title: "CSV file is larger than 1 MB.");
            }

            await csvStream.WriteAsync(buffer.AsMemory(0, bytesRead), context.RequestAborted);
        }

        csvStream.Position = 0;
        string csv;
        using (var reader = new StreamReader(
            csvStream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true))
        {
            csv = await reader.ReadToEndAsync(context.RequestAborted);
        }

        using var parser = new TextFieldParser(new StringReader(csv))
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        try
        {
            var headers = parser.ReadFields();
            if (headers is null || !headers.Select(header => header.Trim())
                    .SequenceEqual(ImportHeaders, StringComparer.OrdinalIgnoreCase))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["header"] = [$"Expected these columns in order: {string.Join(",", ImportHeaders)}."]
                });
            }

            var applications = new List<JobApplication>();
            var errors = new Dictionary<string, string[]>();
            var rowNumber = 1;
            while (!parser.EndOfData)
            {
                rowNumber++;
                var fields = parser.ReadFields();

                if (fields is null || fields.Length != ImportHeaders.Length)
                {
                    errors[$"row {rowNumber}"] = [$"Expected {ImportHeaders.Length} columns."];
                    continue;
                }

                if (rowNumber > MaxImportRows + 1)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["file"] = [$"CSV files may contain at most {MaxImportRows} application rows."]
                    });
                }

                var fieldsForRow = fields.Select(field => field.Trim()).ToArray();
                decimal? salary = null;
                if (fieldsForRow[3].Length > 0)
                {
                    if (!decimal.TryParse(fieldsForRow[3], NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedSalary))
                    {
                        errors[$"row {rowNumber}"] = ["Salary must be a number using a period as the decimal separator."];
                        continue;
                    }

                    salary = parsedSalary;
                }

                if (!DateTime.TryParseExact(
                    fieldsForRow[5],
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var applicationDate))
                {
                    errors[$"row {rowNumber}"] = ["ApplicationDate must use YYYY-MM-DD format."];
                    continue;
                }

                var status = ApplicationStatus.Saved;
                if (fieldsForRow[6].Length > 0 &&
                    (!Enum.TryParse(fieldsForRow[6], ignoreCase: true, out status) ||
                     !Enum.IsDefined(status)))
                {
                    errors[$"row {rowNumber}"] = [$"Status must be one of: {string.Join(", ", Enum.GetNames<ApplicationStatus>())}."];
                    continue;
                }

                var applicationRequest = new ApplicationRequest(
                    fieldsForRow[0],
                    fieldsForRow[1],
                    fieldsForRow[2],
                    salary,
                    fieldsForRow[4],
                    applicationDate,
                    status,
                    fieldsForRow[7]);
                var validationErrors = applicationRequest.Validate();
                if (validationErrors.Count > 0)
                {
                    errors[$"row {rowNumber}"] = validationErrors.Values.SelectMany(messages => messages).ToArray();
                    continue;
                }

                var application = new JobApplication { UserId = user.GetUserId() };
                applicationRequest.ApplyTo(application);
                applications.Add(application);
            }

            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            if (applications.Count == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = ["Include at least one application row."]
                });
            }

            dbContext.JobApplications.AddRange(applications);
            await dbContext.SaveChangesAsync(context.RequestAborted);
            return Results.Ok(new ApplicationImportResponse(applications.Count));
        }
        catch (MalformedLineException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [$"line {parser.ErrorLineNumber}"] = ["The CSV row has invalid quoting or delimiters."]
            });
        }
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
