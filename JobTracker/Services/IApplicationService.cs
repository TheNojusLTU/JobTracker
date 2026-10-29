using JobTracker.Contracts;
using JobTracker.Extensions;
using JobTracker.Models;

namespace JobTracker.Services;

public interface IApplicationService
{
    Task<PagedResponse<ApplicationResponse>> ListAsync(
        string userId,
        string? search = null,
        ApplicationStatus? status = null,
        string? location = null,
        DateRange? dates = null,
        bool newestFirst = true,
        int page = 1,
        int pageSize = PagingExtensions.DefaultPageSize);

    Task<ApplicationResponse?> GetAsync(string userId, int id);

    Task<ApplicationResponse> CreateAsync(string userId, ApplicationRequest request);

    Task<ApplicationResponse?> UpdateAsync(string userId, int id, ApplicationRequest request);

    Task<bool> DeleteAsync(string userId, int id);
}
