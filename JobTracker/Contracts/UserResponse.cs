using JobTracker.Data;

namespace JobTracker.Contracts;

public sealed record UserResponse(string Id, string Username, string DisplayName, string? Email, string AvatarKey)
{
    public static UserResponse FromUser(ApplicationUser user) =>
        new(user.Id, user.UserName!, user.DisplayName, user.Email, user.AvatarKey);
}
