using JobTracker.Models;

namespace JobTracker.Contracts;

public record ApplicationResponse(
    int Id,
    string CompanyName,
    string JobTitle,
    string? Location,
    decimal? Salary,
    string? JobUrl,
    DateTime ApplicationDate,
    ApplicationStatus Status,
    string? Notes)
{
    public static ApplicationResponse From(JobApplication application) => new(
        application.Id,
        application.CompanyName,
        application.JobTitle,
        application.Location,
        application.Salary,
        application.JobUrl,
        application.ApplicationDate,
        application.Status,
        application.Notes);
}
