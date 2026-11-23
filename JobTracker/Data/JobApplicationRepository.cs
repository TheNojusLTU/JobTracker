using JobTracker.Api;
using JobTracker.Extensions;
using JobTracker.Models;
using JobTracker.Services;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Data;

public sealed class JobApplicationRepository(ApplicationDbContext dbContext) : IJobApplicationRepository
{
    public async Task<ApplicationStatisticsResponse> GetStatisticsAsync(
        string userId, CancellationToken cancellationToken)
    {
        var counts = await dbContext.JobApplications
            .Where(application => application.UserId == userId)
            .GroupBy(application => new { application.ApplicationDate.Year, application.ApplicationDate.Month, application.Status })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                group.Key.Status,
                Count = group.Count(),
                Interviews = group.Count(application => application.HasReachedInterview),
                Offers = group.Count(application => application.HasReachedOffer),
                InterviewsWithOffers = group.Count(application => application.HasReachedInterview && application.HasReachedOffer)
            })
            .ToListAsync(cancellationToken);

        var statusCounts = counts.GroupBy(bucket => bucket.Status)
            .ToDictionary(group => group.Key, group => group.Sum(bucket => bucket.Count));
        var byStatus = Enum.GetValues<ApplicationStatus>()
            .ToDictionary(status => status, status => statusCounts.GetValueOrDefault(status));
        var byMonth = counts.GroupBy(bucket => (bucket.Year, bucket.Month))
            .OrderByDescending(group => group.Key.Year)
            .ThenByDescending(group => group.Key.Month)
            .Select(group => new MonthlyApplicationCount(group.Key.Year, group.Key.Month, group.Sum(bucket => bucket.Count)))
            .ToArray();
        var totalCount = counts.Sum(bucket => bucket.Count);
        var interviewCount = counts.Sum(bucket => bucket.Interviews);

        return new ApplicationStatisticsResponse(
            totalCount,
            byStatus,
            byMonth,
            interviewCount,
            counts.Sum(bucket => bucket.Offers),
            CalculateRate(interviewCount, totalCount),
            CalculateRate(counts.Sum(bucket => bucket.InterviewsWithOffers), interviewCount));
    }

    public async Task<(IReadOnlyList<JobApplication> Items, int TotalCount)> ListAsync(
        string userId,
        string? search,
        ApplicationStatus? status,
        string? location,
        DateRange? dates,
        bool? priority,
        bool newestFirst,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var result = await dbContext.JobApplications
            .AsNoTracking()
            .Where(application => application.UserId == userId)
            .Search(search)
            .Filter(status: status, location: location, dates: dates, priority: priority)
            .SortByDate(newestFirst)
            .ToPagedResponseAsync(page, pageSize, application => application, cancellationToken);

        return (result.Items, result.TotalCount);
    }

    public Task<JobApplication?> GetAsync(int id, string userId, CancellationToken cancellationToken) =>
        FindOwnedAsync(dbContext.JobApplications.AsNoTracking(), id, userId, cancellationToken);

    public Task<JobApplication?> GetForUpdateAsync(int id, string userId, CancellationToken cancellationToken) =>
        FindOwnedAsync(dbContext.JobApplications, id, userId, cancellationToken);

    public async Task<bool> UpdateFlagsAsync(
        int id, ApplicationFlagsRequest request, string userId, CancellationToken cancellationToken)
    {
        var application = await GetForUpdateAsync(id, userId, cancellationToken);
        if (application is null)
        {
            return false;
        }

        if (request.IsFavorite is { } isFavorite)
        {
            application.IsFavorite = isFavorite;
        }

        if (request.IsPriority is { } isPriority)
        {
            application.IsPriority = isPriority;
        }

        await SaveChangesAsync(cancellationToken);
        return true;
    }

    public void Add(JobApplication application) => dbContext.JobApplications.Add(application);

    public void Remove(JobApplication application) => dbContext.JobApplications.Remove(application);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await dbContext.SaveChangesAsync(cancellationToken);

    private static Task<JobApplication?> FindOwnedAsync(
        IQueryable<JobApplication> applications,
        int id,
        string userId,
        CancellationToken cancellationToken) =>
        applications.SingleOrDefaultAsync(
            application => application.Id == id && application.UserId == userId,
            cancellationToken);

    private static decimal? CalculateRate(int count, int total) => total == 0 ? null : (decimal)count / total;
}
