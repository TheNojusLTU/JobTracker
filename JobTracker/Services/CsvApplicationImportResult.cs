namespace JobTracker.Services;

public sealed record CsvApplicationImportResult(
    int? ImportedCount,
    Dictionary<string, string[]>? Errors,
    bool IsFileTooLarge)
{
    public static CsvApplicationImportResult Imported(int count) => new(count, null, false);

    public static CsvApplicationImportResult Invalid(Dictionary<string, string[]> errors) =>
        new(null, errors, false);

    public static CsvApplicationImportResult FileTooLarge() => new(null, null, true);
}
