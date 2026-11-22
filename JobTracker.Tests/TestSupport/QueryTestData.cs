using JobTracker.Models;

namespace JobTracker.Tests.TestSupport;

internal static class QueryTestData
{
    public static List<JobApplication> Applications =>
    [
        new() { Id = 1, CompanyName = "Acme", JobTitle = "Backend Developer", Location = "Vilnius", Status = ApplicationStatus.Applied, ApplicationDate = new DateTime(2026, 9, 1) },
        new() { Id = 2, CompanyName = "Globex", JobTitle = "Frontend Developer", Location = "Kaunas", Status = ApplicationStatus.Interview, ApplicationDate = new DateTime(2026, 10, 1) },
        new() { Id = 3, CompanyName = "Initech", JobTitle = "QA Engineer", Location = null, Status = ApplicationStatus.Applied, ApplicationDate = new DateTime(2026, 10, 1) },
        new() { Id = 4, CompanyName = "Umbrella", JobTitle = "Data Analyst", Location = "vilnius", Status = ApplicationStatus.Rejected, ApplicationDate = new DateTime(2026, 11, 1) }
    ];

    public static int[] Ids(IEnumerable<JobApplication> applications) => applications.Select(a => a.Id).ToArray();
}
