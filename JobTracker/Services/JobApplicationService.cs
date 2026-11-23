using JobTracker.Api;
using JobTracker.Data;
using JobTracker.Extensions;
using JobTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Services;

public sealed class JobApplicationService(ApplicationDbContext dbContext) : IJobApplicationService
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

    private static decimal? CalculateRate(int count, int total) => total == 0 ? null : (decimal)count / total;

    public async Task<PagedResponse<ApplicationResponse>> GetApplicationsAsync(
        string userId,
        string? search,
        ApplicationStatus? status,
        string? location,
        DateRange? dates,
        bool newestFirst,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        return await dbContext.JobApplications
            .AsNoTracking()
            .Where(application => application.UserId == userId)
            .Search(search)
            .Filter(status: status, location: location, dates: dates)
            .SortByDate(newestFirst)
            .ToPagedResponseAsync(page, pageSize, ApplicationResponse.From, cancellationToken);
    }

    public async Task<ApplicationResponse?> GetApplicationAsync(
        int id,
        string userId,
        CancellationToken cancellationToken)
    {
        var application = await FindOwnedAsync(
            dbContext.JobApplications.AsNoTracking(), id, userId, cancellationToken);

        return application is null ? null : ApplicationResponse.From(application);
    }

    public async Task<ApplicationResponse> CreateApplicationAsync(
        ApplicationRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        var application = new JobApplication { UserId = userId };
        request.ApplyTo(application);

        dbContext.JobApplications.Add(application);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResponse.From(application);
    }

    public async Task<ApplicationResponse?> UpdateApplicationAsync(
        int id,
        ApplicationRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        var application = await FindOwnedAsync(
            dbContext.JobApplications, id, userId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        request.ApplyTo(application);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResponse.From(application);
    }

    public async Task<bool> DeleteApplicationAsync(
        int id,
        string userId,
        CancellationToken cancellationToken)
    {
        var application = await FindOwnedAsync(
            dbContext.JobApplications, id, userId, cancellationToken);
        if (application is null)
        {
            return false;
        }

        dbContext.JobApplications.Remove(application);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static Task<JobApplication?> FindOwnedAsync(
        IQueryable<JobApplication> applications,
        int id,
        string userId,
        CancellationToken cancellationToken) =>
        applications.SingleOrDefaultAsync(
            application => application.Id == id && application.UserId == userId,
            cancellationToken);
}
