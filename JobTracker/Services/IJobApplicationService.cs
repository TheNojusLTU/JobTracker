using JobTracker.Api;
using JobTracker.Models;

namespace JobTracker.Services;

public interface IJobApplicationService
{
    Task<ApplicationStatisticsResponse> GetStatisticsAsync(string userId, CancellationToken cancellationToken);

    Task<PagedResponse<ApplicationResponse>> GetApplicationsAsync(
        string userId,
        string? search,
        ApplicationStatus? status,
        string? location,
        DateRange? dates,
        bool newestFirst,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ApplicationResponse?> GetApplicationAsync(int id, string userId, CancellationToken cancellationToken);

    Task<ApplicationResponse> CreateApplicationAsync(
        ApplicationRequest request, string userId, CancellationToken cancellationToken);

    Task<ApplicationResponse?> UpdateApplicationAsync(
        int id, ApplicationRequest request, string userId, CancellationToken cancellationToken);

    Task<bool> DeleteApplicationAsync(int id, string userId, CancellationToken cancellationToken);
}
