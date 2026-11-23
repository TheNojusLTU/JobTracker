using JobTracker.Models;

namespace JobTracker.Extensions;

public static class JobApplicationFilterExtensions
{
    public static IQueryable<JobApplication> Filter(
        this IQueryable<JobApplication> applications,
        ApplicationStatus? status = null,
        string? location = null,
        DateRange? dates = null,
        bool? priority = null)
    {
        ArgumentNullException.ThrowIfNull(applications);

        var result = applications;

        if (status is not null)
        {
            result = result.Where(a => a.Status == status);
        }

        if (priority is { } isPriority)
        {
            result = result.Where(a => a.IsPriority == isPriority);
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var place = location.Trim().ToLower();
            result = result.Where(a => a.Location != null && a.Location.ToLower().Contains(place));
        }

        if (dates is { } range)
        {
            if (range.StartUtc is { } start)
            {
                result = result.Where(a => a.ApplicationDate >= start);
            }

            if (range.EndUtc is { } end)
            {
                result = result.Where(a => a.ApplicationDate < end);
            }
        }

        return result;
    }

    public static IEnumerable<JobApplication> Filter(
        this IEnumerable<JobApplication> applications,
        ApplicationStatus? status = null,
        string? location = null,
        DateRange? dates = null,
        bool? priority = null)
    {
        ArgumentNullException.ThrowIfNull(applications);

        return applications.AsQueryable().Filter(status, location, dates, priority);
    }
}
