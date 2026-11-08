using JobTracker.Api;
using JobTracker.Models;

namespace JobTracker.Tests.Unit;

public class ApplicationRequestTests
{
    private static ApplicationRequest Valid() => new(
        "Acme", "Backend Developer", "Vilnius", 2500m, "https://acme.lt/jobs/1",
        new DateTime(2026, 10, 1), ApplicationStatus.Applied, "Sent CV");

    [Fact]
    public void Validate_ValidRequest_HasNoErrors()
    {
        Assert.Empty(Valid().Validate(requireStatus: true));
    }

    [Fact]
    public void Validate_EmptyRequest_ListsRequiredFields()
    {
        var request = new ApplicationRequest(null, null, null, null, null, null);

        var errors = request.Validate();

        Assert.Equal(["CompanyName", "JobTitle", "ApplicationDate"], errors.Keys);
    }

    [Theory]
    [InlineData("http://acme.lt")]
    [InlineData("https://acme.lt/jobs?id=1")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_AcceptsHttpUrlsOrNoUrl(string? url)
    {
        Assert.Empty((Valid() with { JobUrl = url }).Validate());
    }

    [Theory]
    [InlineData("ftp://acme.lt")]
    [InlineData("acme.lt")]
    [InlineData("javascript:alert(1)")]
    public void Validate_RejectsOtherUrls(string url)
    {
        Assert.Equal(["JobUrl"], (Valid() with { JobUrl = url }).Validate().Keys);
    }

    [Fact]
    public void Validate_RejectsTooLongFieldsAndNegativeSalary()
    {
        var request = Valid() with
        {
            CompanyName = new string('a', 201),
            JobTitle = new string('a', 201),
            Location = new string('a', 201),
            Salary = -1,
            Notes = new string('a', 5001)
        };

        Assert.Equal(["CompanyName", "JobTitle", "Location", "Salary", "Notes"], request.Validate().Keys);
    }

    [Fact]
    public void Validate_StatusIsOnlyRequiredWhenAsked()
    {
        var request = Valid() with { Status = null };

        Assert.Empty(request.Validate());
        Assert.Equal(["Status"], request.Validate(requireStatus: true).Keys);
    }

    [Fact]
    public void Validate_RejectsUndefinedStatus()
    {
        var request = Valid() with { Status = (ApplicationStatus)99 };

        Assert.Equal(["Status"], request.Validate().Keys);
    }

    [Fact]
    public void ApplyTo_TrimsTextAndStoresUtcDate()
    {
        var request = new ApplicationRequest(" Acme ", " Dev ", "  ", null, " ", new DateTime(2026, 10, 1, 15, 30, 0), null, " note ");
        var application = new JobApplication();

        request.ApplyTo(application);

        Assert.Equal("Acme", application.CompanyName);
        Assert.Equal("Dev", application.JobTitle);
        Assert.Null(application.Location);
        Assert.Null(application.JobUrl);
        Assert.Equal("note", application.Notes);
        Assert.Equal(ApplicationStatus.Saved, application.Status);
        Assert.Equal(new DateTime(2026, 10, 1), application.ApplicationDate);
        Assert.Equal(DateTimeKind.Utc, application.ApplicationDate.Kind);
    }

    [Fact]
    public void ApplicationResponse_From_CopiesEveryField()
    {
        var application = new JobApplication
        {
            Id = 7,
            CompanyName = "Acme",
            JobTitle = "Dev",
            Location = "Vilnius",
            Salary = 10m,
            JobUrl = "https://acme.lt",
            ApplicationDate = new DateTime(2026, 10, 1),
            Status = ApplicationStatus.Offer,
            Notes = "n"
        };

        var response = ApplicationResponse.From(application);

        Assert.Equal(new ApplicationResponse(7, "Acme", "Dev", "Vilnius", 10m, "https://acme.lt", new DateTime(2026, 10, 1), ApplicationStatus.Offer, "n"), response);
    }
}
