using System.Globalization;
using System.Text;
using JobTracker.Api;
using JobTracker.Data;
using JobTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;

namespace JobTracker.Services;

public sealed class CsvApplicationImportService(ApplicationDbContext dbContext)
{
    public const long MaxImportBytes = 1_048_576;
    private const int MaxImportRows = 500;
    private static readonly string[] ImportHeaders =
    [
        "CompanyName", "JobTitle", "Location", "Salary",
        "JobUrl", "ApplicationDate", "Status", "Notes"
    ];

    public async Task<CsvApplicationImportResult> ImportAsync(
        Stream csvStream,
        string userId,
        CancellationToken cancellationToken)
    {
        var content = await ReadLimitedAsync(csvStream, cancellationToken);
        if (content is null)
        {
            return CsvApplicationImportResult.FileTooLarge();
        }

        using var parser = new TextFieldParser(new StringReader(content))
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        try
        {
            return await ParseAndSaveAsync(parser, userId, cancellationToken);
        }
        catch (MalformedLineException)
        {
            return CsvApplicationImportResult.Invalid(new Dictionary<string, string[]>
            {
                [$"line {parser.ErrorLineNumber}"] = ["The CSV row has invalid quoting or delimiters."]
            });
        }
    }

    private async Task<CsvApplicationImportResult> ParseAndSaveAsync(
        TextFieldParser parser,
        string userId,
        CancellationToken cancellationToken)
    {
        var headers = parser.ReadFields();
        if (headers is null || !headers.Select(header => header.Trim())
                .SequenceEqual(ImportHeaders, StringComparer.OrdinalIgnoreCase))
        {
            return CsvApplicationImportResult.Invalid(new Dictionary<string, string[]>
            {
                ["header"] = [$"Expected these columns in order: {string.Join(",", ImportHeaders)}."]
            });
        }

        var applications = new List<JobApplication>();
        var errors = new Dictionary<string, string[]>();
        var rowNumber = 1;
        while (!parser.EndOfData)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;
            var fields = parser.ReadFields();

            if (fields is null || fields.Length != ImportHeaders.Length)
            {
                errors[$"row {rowNumber}"] = [$"Expected {ImportHeaders.Length} columns."];
                continue;
            }

            if (rowNumber > MaxImportRows + 1)
            {
                return CsvApplicationImportResult.Invalid(new Dictionary<string, string[]>
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

            var request = new ApplicationRequest(
                fieldsForRow[0],
                fieldsForRow[1],
                fieldsForRow[2],
                salary,
                fieldsForRow[4],
                applicationDate,
                status,
                fieldsForRow[7]);
            var validationErrors = request.Validate();
            if (validationErrors.Count > 0)
            {
                errors[$"row {rowNumber}"] = validationErrors.Values.SelectMany(messages => messages).ToArray();
                continue;
            }

            var application = new JobApplication { UserId = userId };
            request.ApplyTo(application);
            applications.Add(application);
        }

        if (errors.Count > 0)
        {
            return CsvApplicationImportResult.Invalid(errors);
        }

        if (applications.Count == 0)
        {
            return CsvApplicationImportResult.Invalid(new Dictionary<string, string[]>
            {
                ["file"] = ["Include at least one application row."]
            });
        }

        dbContext.JobApplications.AddRange(applications);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CsvApplicationImportResult.Imported(applications.Count);
    }

    private static async Task<string?> ReadLimitedAsync(
        Stream input,
        CancellationToken cancellationToken)
    {
        using var csvStream = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var remainingBytes = (int)(MaxImportBytes - csvStream.Length + 1);
            var bytesRead = await input.ReadAsync(
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
        using var reader = new StreamReader(
            csvStream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
