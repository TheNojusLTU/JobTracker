using JobTracker.Api;
using JobTracker.Models;
using JobTracker.Services;

namespace JobTracker.Tests.Services;

public class JobApplicationServiceTests
{
    [Fact]
    public async Task GetApplications_ForwardsQueryAndMapsResults()
    {
        var repository = new FakeRepository();
        repository.ListResult = ([new JobApplication { Id = 7, CompanyName = "Acme", UserId = "owner" }], 12);
        var service = new JobApplicationService(repository);
        var dates = new DateRange(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));
        using var cancellation = new CancellationTokenSource();

        var result = await service.GetApplicationsAsync(
            "owner", "developer", ApplicationStatus.Applied, "Vilnius", dates, false, 2, 5, cancellation.Token);

        Assert.Equal(("owner", "developer", ApplicationStatus.Applied, "Vilnius", dates, false, 2, 5, cancellation.Token),
            repository.ListArguments);
        Assert.Equal(12, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.PageSize);
        Assert.Equal("Acme", Assert.Single(result.Items).CompanyName);
    }

    [Fact]
    public async Task CreateUpdateAndDelete_UseRepositoryAndSaveChanges()
    {
        var repository = new FakeRepository();
        var service = new JobApplicationService(repository);
        using var cancellation = new CancellationTokenSource();

        var created = await service.CreateApplicationAsync(Request("  Acme  "), "owner", cancellation.Token);
        Assert.Equal("Acme", created.CompanyName);
        Assert.Equal("owner", repository.Application!.UserId);
        Assert.Equal(ApplicationStatus.Saved, created.Status);
        Assert.Equal(cancellation.Token, repository.LastSaveToken);
        Assert.Equal(1, repository.SaveCount);

        var updated = await service.UpdateApplicationAsync(
            created.Id, Request("Globex", ApplicationStatus.Interview), "owner", cancellation.Token);
        Assert.Equal("Globex", updated!.CompanyName);
        Assert.Equal(ApplicationStatus.Interview, updated.Status);
        Assert.Equal(2, repository.SaveCount);

        Assert.True(await service.DeleteApplicationAsync(created.Id, "owner", cancellation.Token));
        Assert.Null(repository.Application);
        Assert.Equal(3, repository.SaveCount);
    }

    [Fact]
    public async Task OtherUsersApplication_IsNotReturnedOrChanged()
    {
        var repository = new FakeRepository
        {
            Application = new JobApplication { Id = 7, UserId = "owner", CompanyName = "Acme" }
        };
        var service = new JobApplicationService(repository);

        Assert.Null(await service.GetApplicationAsync(7, "other", CancellationToken.None));
        Assert.Null(await service.UpdateApplicationAsync(7, Request("Changed"), "other", CancellationToken.None));
        Assert.False(await service.DeleteApplicationAsync(7, "other", CancellationToken.None));
        Assert.Equal("Acme", repository.Application!.CompanyName);
        Assert.Equal(0, repository.SaveCount);
    }

    private static ApplicationRequest Request(string company, ApplicationStatus? status = null) =>
        new(company, "Developer", null, null, null, new DateTime(2026, 10, 1), status);

    private sealed class FakeRepository : IJobApplicationRepository
    {
        public JobApplication? Application { get; set; }
        public int SaveCount { get; private set; }
        public CancellationToken LastSaveToken { get; private set; }
        public (IReadOnlyList<JobApplication> Items, int TotalCount) ListResult { get; set; }
        public (string UserId, string? Search, ApplicationStatus? Status, string? Location, DateRange? Dates,
            bool NewestFirst, int Page, int PageSize, CancellationToken CancellationToken) ListArguments
        { get; private set; }

        public Task<(IReadOnlyList<JobApplication> Items, int TotalCount)> ListAsync(
            string userId, string? search, ApplicationStatus? status, string? location, DateRange? dates,
            bool newestFirst, int page, int pageSize, CancellationToken cancellationToken)
        {
            ListArguments = (userId, search, status, location, dates, newestFirst, page, pageSize, cancellationToken);
            return Task.FromResult(ListResult);
        }

        public Task<JobApplication?> GetAsync(int id, string userId, CancellationToken cancellationToken) =>
            Task.FromResult(Application is { } application && application.Id == id && application.UserId == userId
                ? application : null);

        public Task<JobApplication?> GetForUpdateAsync(int id, string userId, CancellationToken cancellationToken) =>
            GetAsync(id, userId, cancellationToken);

        public void Add(JobApplication application)
        {
            application.Id = 7;
            Application = application;
        }

        public void Remove(JobApplication application) => Application = null;

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            LastSaveToken = cancellationToken;
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
