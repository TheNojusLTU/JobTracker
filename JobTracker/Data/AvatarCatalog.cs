namespace JobTracker.Data;

public static class AvatarCatalog
{
    public static IReadOnlyList<AvatarOption> All { get; } =
    [
        new("sunrise", "Sunrise", "/avatars/sunrise.svg"),
        new("berry", "Berry", "/avatars/berry.svg"),
        new("meadow", "Meadow", "/avatars/meadow.svg"),
        new("ocean", "Ocean", "/avatars/ocean.svg")
    ];

    public static bool Contains(string? key) =>
        All.Any(avatar => string.Equals(avatar.Key, key, StringComparison.Ordinal));

    public static AvatarOption Default => All[0];
}