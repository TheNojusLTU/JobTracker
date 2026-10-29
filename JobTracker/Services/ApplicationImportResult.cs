namespace JobTracker.Services;

public sealed record ApplicationImportResult(int ImportedCount, Dictionary<string, string[]> Errors, bool TooLarge = false)
{
    public static ApplicationImportResult Success(int importedCount) => new(importedCount, new());

    public static ApplicationImportResult Invalid(Dictionary<string, string[]> errors) => new(0, errors);

    public static ApplicationImportResult Invalid(string key, string message) => new(0, new() { [key] = [message] });

    public static ApplicationImportResult FileTooLarge() => new(0, new(), TooLarge: true);
}
