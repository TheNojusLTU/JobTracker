using JobTracker.Api;
using JobTracker.Models;

namespace JobTracker.Services;

public sealed class JobApplicationService(IJobApplicationRepository repository) : IJobApplicationService
{
    public async Task<ApplicationStatisticsResponse> GetStatisticsAsync(
        string userId, CancellationToken cancellationToken)
        => await repository.GetStatisticsAsync(userId, cancellationToken);

    public async Task<PagedResponse<ApplicationResponse>> GetApplicationsAsync(
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
        var (items, totalCount) = await repository.ListAsync(
            userId, search, status, location, dates, priority, newestFirst, page, pageSize, cancellationToken);
        return new PagedResponse<ApplicationResponse>(
            items.Select(ApplicationResponse.From).ToList(), page, pageSize, totalCount);
    }

    public async Task<ApplicationResponse?> GetApplicationAsync(
        int id,
        string userId,
        CancellationToken cancellationToken)
    {
        var application = await repository.GetAsync(id, userId, cancellationToken);

        return application is null ? null : ApplicationResponse.From(application);
    }

    public async Task<ApplicationResponse> CreateApplicationAsync(
        ApplicationRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        var application = new JobApplication { UserId = userId };
        request.ApplyTo(application);

        repository.Add(application);
        await repository.SaveChangesAsync(cancellationToken);

        return ApplicationResponse.From(application);
    }

    public async Task<ApplicationResponse?> UpdateApplicationAsync(
        int id,
        ApplicationRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        var application = await repository.GetForUpdateAsync(id, userId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        request.ApplyTo(application);
        await repository.SaveChangesAsync(cancellationToken);

        return ApplicationResponse.From(application);
    }

    public async Task<bool> UpdateFlagsAsync(
        int id, ApplicationFlagsRequest request, string userId, CancellationToken cancellationToken)
        => await repository.UpdateFlagsAsync(id, request, userId, cancellationToken);

    public async Task<bool> DeleteApplicationAsync(
        int id,
        string userId,
        CancellationToken cancellationToken)
    {
        var application = await repository.GetForUpdateAsync(id, userId, cancellationToken);
        if (application is null)
        {
            return false;
        }

        repository.Remove(application);
        await repository.SaveChangesAsync(cancellationToken);

        return true;
    }
}
