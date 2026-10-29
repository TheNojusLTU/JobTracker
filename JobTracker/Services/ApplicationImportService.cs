using System.Globalization;
using System.Text;
using JobTracker.Contracts;
using JobTracker.Data;
using JobTracker.Models;
using Microsoft.VisualBasic.FileIO;

namespace JobTracker.Services;

public class ApplicationImportService(ApplicationDbContext dbContext) : IApplicationImportService
{
    public const long MaxImportBytes = 1_048_576;
    public const int MaxImportRows = 500;
    private static readonly string[] ImportHeaders =
    [
        "CompanyName", "JobTitle", "Location", "Salary",
        "JobUrl", "ApplicationDate", "Status", "Notes"
    ];

    public async Task<ApplicationImportResult> ImportAsync(string userId, Stream csv, CancellationToken cancellationToken = default)
    {
        var text = await ReadLimitedAsync(csv, cancellationToken);
        if (text is null)
        {
            return ApplicationImportResult.FileTooLarge();
        }

        using var parser = new TextFieldParser(new StringReader(text))
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
                return ApplicationImportResult.Invalid("header", $"Expected these columns in order: {string.Join(",", ImportHeaders)}.");
            }

            var applications = new List<JobApplication>();
            var errors = new Dictionary<string, string[]>();
            foreach (var (rowNumber, fields) in ReadRows(parser))
            {
                if (fields is null || fields.Length != ImportHeaders.Length)
                {
                    errors[$"row {rowNumber}"] = [$"Expected {ImportHeaders.Length} columns."];
                    continue;
                }

                if (rowNumber > MaxImportRows + 1)
                {
                    return ApplicationImportResult.Invalid("file", $"CSV files may contain at most {MaxImportRows} application rows.");
                }

                var (request, rowErrors) = ParseRow(fields);
                if (request is null)
                {
                    errors[$"row {rowNumber}"] = rowErrors;
                    continue;
                }

                var application = new JobApplication { UserId = userId };
                request.ApplyTo(application);
                applications.Add(application);
            }

            if (errors.Count > 0)
            {
                return ApplicationImportResult.Invalid(errors);
            }

            if (applications.Count == 0)
            {
                return ApplicationImportResult.Invalid("file", "Include at least one application row.");
            }

            dbContext.JobApplications.AddRange(applications);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ApplicationImportResult.Success(applications.Count);
        }
        catch (MalformedLineException)
        {
            return ApplicationImportResult.Invalid($"line {parser.ErrorLineNumber}", "The CSV row has invalid quoting or delimiters.");
        }
    }

    // reads the stream in chunks so a huge body is never fully buffered, null means it was over the limit
    private static async Task<string?> ReadLimitedAsync(Stream body, CancellationToken cancellationToken)
    {
        using var csvStream = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var remainingBytes = (int)(MaxImportBytes - csvStream.Length + 1);
            var bytesRead = await body.ReadAsync(
                buffer.AsMemory(0, Math.Min(buffer.Length, remainingBytes)),
                cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            if (csvStream.Length + bytesRead > MaxImportBytes)
            {
                return null;
            }

            await csvStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        csvStream.Position = 0;
        using var reader = new StreamReader(csvStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    // rows are read one at a time while the caller loops, row 1 is the header
    private static IEnumerable<(int RowNumber, string[]? Fields)> ReadRows(TextFieldParser parser)
    {
        var rowNumber = 1;
        while (!parser.EndOfData)
        {
            rowNumber++;
            yield return (rowNumber, parser.ReadFields());
        }
    }

    private static (ApplicationRequest? Request, string[] Errors) ParseRow(string[] rawFields)
    {
        var fields = rawFields.Select(field => field.Trim()).ToArray();

        decimal? salary = null;
        if (fields[3].Length > 0)
        {
            if (!decimal.TryParse(fields[3], NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedSalary))
            {
                return (null, ["Salary must be a number using a period as the decimal separator."]);
            }

            salary = parsedSalary;
        }

        if (!DateTime.TryParseExact(fields[5], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var applicationDate))
        {
            return (null, ["ApplicationDate must use YYYY-MM-DD format."]);
        }

        var status = ApplicationStatus.Saved;
        if (fields[6].Length > 0 &&
            (!Enum.TryParse(fields[6], ignoreCase: true, out status) || !Enum.IsDefined(status)))
        {
            return (null, [$"Status must be one of: {string.Join(", ", Enum.GetNames<ApplicationStatus>())}."]);
        }

        var request = new ApplicationRequest(
            CompanyName: fields[0],
            JobTitle: fields[1],
            Location: fields[2],
            Salary: salary,
            JobUrl: fields[4],
            ApplicationDate: applicationDate,
            Status: status,
            Notes: fields[7]);
        var errors = request.Validate();

        return errors.Count > 0 ? (null, errors.Values.SelectMany(messages => messages).ToArray()) : (request, []);
    }
}
