using JobTracker.Contracts;
using JobTracker.Data;
using JobTracker.Extensions;
using JobTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Services;

public class ApplicationService(ApplicationDbContext dbContext) : IApplicationService
{
    public Task<PagedResponse<ApplicationResponse>> ListAsync(
        string userId,
        string? search = null,
        ApplicationStatus? status = null,
        string? location = null,
        DateRange? dates = null,
        bool newestFirst = true,
        int page = 1,
        int pageSize = PagingExtensions.DefaultPageSize)
    {
        // everything up to the paging runs as one SQL query, nothing is loaded before that
        return dbContext.JobApplications
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .Search(search)
            .Filter(status: status, location: location, dates: dates)
            .SortByDate(newestFirst)
            .ToPagedResponseAsync(page, pageSize, ApplicationResponse.From);
    }

    public async Task<ApplicationResponse?> GetAsync(string userId, int id)
    {
        var application = await FindOwnedAsync(dbContext.JobApplications.AsNoTracking(), userId, id);

        return application is null ? null : ApplicationResponse.From(application);
    }

    public async Task<ApplicationResponse> CreateAsync(string userId, ApplicationRequest request)
    {
        var application = new JobApplication { UserId = userId };
        request.ApplyTo(application);

        dbContext.JobApplications.Add(application);
        await dbContext.SaveChangesAsync();

        return ApplicationResponse.From(application);
    }

    public async Task<ApplicationResponse?> UpdateAsync(string userId, int id, ApplicationRequest request)
    {
        var application = await FindOwnedAsync(dbContext.JobApplications, userId, id);
        if (application is null)
        {
            return null;
        }

        request.ApplyTo(application);
        await dbContext.SaveChangesAsync();

        return ApplicationResponse.From(application);
    }

    public async Task<bool> DeleteAsync(string userId, int id)
    {
        var application = await FindOwnedAsync(dbContext.JobApplications, userId, id);
        if (application is null)
        {
            return false;
        }

        dbContext.JobApplications.Remove(application);
        await dbContext.SaveChangesAsync();

        return true;
    }

    // other users' applications are treated as missing, so ids of other users can't be probed
    private static Task<JobApplication?> FindOwnedAsync(IQueryable<JobApplication> applications, string userId, int id) =>
        applications.SingleOrDefaultAsync(a => a.Id == id && a.UserId == userId);
}
