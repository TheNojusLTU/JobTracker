using JobTracker.Data;

namespace JobTracker.Models;

public class JobApplication
{
    public int Id { get; set; }

    public string CompanyName { get; set; } = string.Empty;

    public string JobTitle { get; set; } = string.Empty;

    public string? Location { get; set; }

    public decimal? Salary { get; set; }

    public string? JobUrl { get; set; }

    public DateTime ApplicationDate { get; set; }

    public ApplicationStatus Status { get; set; }

    public bool HasReachedInterview { get; private set; }

    public bool HasReachedOffer { get; private set; }

    public string? Notes { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    public void RecordStatus(ApplicationStatus status)
    {
        Status = status;
        HasReachedInterview |= status == ApplicationStatus.Interview;
        HasReachedOffer |= status == ApplicationStatus.Offer;
    }
}
