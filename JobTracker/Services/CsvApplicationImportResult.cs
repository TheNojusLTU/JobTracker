namespace JobTracker.Services;

public sealed record CsvApplicationImportResult(
    int? ImportedCount,
    Dictionary<string, string[]>? Errors,
    bool IsFileTooLarge,
    bool IsImportInProgress)
{
    public static CsvApplicationImportResult Imported(int count) => new(count, null, false, false);

    public static CsvApplicationImportResult Invalid(Dictionary<string, string[]> errors) =>
        new(null, errors, false, false);

    public static CsvApplicationImportResult FileTooLarge() => new(null, null, true, false);

    public static CsvApplicationImportResult ImportInProgress() => new(null, null, false, true);
}
