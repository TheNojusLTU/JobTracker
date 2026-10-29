using JobTracker.Models;

namespace JobTracker.Contracts;

public record ApplicationRequest(
    string? CompanyName,
    string? JobTitle,
    string? Location,
    decimal? Salary,
    string? JobUrl,
    DateTime? ApplicationDate,
    ApplicationStatus? Status = null,
    string? Notes = null)
{
    public Dictionary<string, string[]> Validate(bool requireStatus = false)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(CompanyName) || CompanyName.Trim().Length > 200)
        {
            errors[nameof(CompanyName)] = ["Company name is required and must be 200 characters or fewer."];
        }

        if (string.IsNullOrWhiteSpace(JobTitle) || JobTitle.Trim().Length > 200)
        {
            errors[nameof(JobTitle)] = ["Job title is required and must be 200 characters or fewer."];
        }

        if (Location?.Trim().Length > 200)
        {
            errors[nameof(Location)] = ["Location must be 200 characters or fewer."];
        }

        if (Salary < 0)
        {
            errors[nameof(Salary)] = ["Salary must be a non-negative number."];
        }

        if (!string.IsNullOrWhiteSpace(JobUrl) && !IsHttpUrl(JobUrl.Trim()))
        {
            errors[nameof(JobUrl)] = ["Enter a valid http or https job posting URL."];
        }

        if (ApplicationDate is null)
        {
            errors[nameof(ApplicationDate)] = ["Enter a valid application date."];
        }

        if (Status is null && requireStatus)
        {
            errors[nameof(Status)] = ["Status is required when updating an application."];
        }
        else if (Status is not null && !Enum.IsDefined(Status.Value))
        {
            errors[nameof(Status)] = ["Choose a valid application status."];
        }

        if (Notes?.Trim().Length > 5000)
        {
            errors[nameof(Notes)] = ["Notes must be 5,000 characters or fewer."];
        }

        return errors;
    }

    public void ApplyTo(JobApplication application)
    {
        application.CompanyName = CompanyName!.Trim();
        application.JobTitle = JobTitle!.Trim();
        application.Location = string.IsNullOrWhiteSpace(Location) ? null : Location.Trim();
        application.Salary = Salary;
        application.JobUrl = string.IsNullOrWhiteSpace(JobUrl) ? null : JobUrl.Trim();
        // postgres timestamptz only accepts UTC, same as the old form handler
        application.ApplicationDate = DateTime.SpecifyKind(ApplicationDate!.Value.Date, DateTimeKind.Utc);
        // new applications start as Saved, updates must send a status (checked in Validate)
        application.Status = Status ?? ApplicationStatus.Saved;
        application.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
    }

    private static bool IsHttpUrl(string url) =>
        url.Length <= 2048 &&
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
