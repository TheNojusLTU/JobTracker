namespace JobTracker.Contracts;

public sealed record RegisterRequest(
    string? Username,
    string? DisplayName,
    string? Password,
    string? Email = null,
    string? AvatarKey = null);
