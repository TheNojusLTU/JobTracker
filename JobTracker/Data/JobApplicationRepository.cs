using JobTracker.Api;
using JobTracker.Extensions;
using JobTracker.Models;
using JobTracker.Services;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Data;

public sealed class JobApplicationRepository(ApplicationDbContext dbContext) : IJobApplicationRepository
{
    public async Task<(IReadOnlyList<JobApplication> Items, int TotalCount)> ListAsync(
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
        var result = await dbContext.JobApplications
            .AsNoTracking()
            .Where(application => application.UserId == userId)
            .Search(search)
            .Filter(status: status, location: location, dates: dates)
            .SortByDate(newestFirst)
            .ToPagedResponseAsync(page, pageSize, application => application, cancellationToken);

        return (result.Items, result.TotalCount);
    }

    public Task<JobApplication?> GetAsync(int id, string userId, CancellationToken cancellationToken) =>
        FindOwnedAsync(dbContext.JobApplications.AsNoTracking(), id, userId, cancellationToken);

    public Task<JobApplication?> GetForUpdateAsync(int id, string userId, CancellationToken cancellationToken) =>
        FindOwnedAsync(dbContext.JobApplications, id, userId, cancellationToken);

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
}
