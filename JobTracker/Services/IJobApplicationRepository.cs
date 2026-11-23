using JobTracker.Api;
using JobTracker.Models;

namespace JobTracker.Services;

public interface IJobApplicationRepository
{
    Task<ApplicationStatisticsResponse> GetStatisticsAsync(
        string userId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<JobApplication> Items, int TotalCount)> ListAsync(
        string userId,
        string? search,
        ApplicationStatus? status,
        string? location,
        DateRange? dates,
        bool? priority,
        bool newestFirst,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<JobApplication?> GetAsync(int id, string userId, CancellationToken cancellationToken);

    Task<JobApplication?> GetForUpdateAsync(int id, string userId, CancellationToken cancellationToken);

    Task<bool> UpdateFlagsAsync(
        int id, ApplicationFlagsRequest request, string userId, CancellationToken cancellationToken);

    void Add(JobApplication application);

    void Remove(JobApplication application);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
