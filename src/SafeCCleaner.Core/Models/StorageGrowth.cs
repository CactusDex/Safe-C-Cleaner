namespace SafeCCleaner.Core.Models;

public sealed record StorageGrowth(
    string Key,
    string DisplayName,
    string Path,
    long CurrentBytes,
    long BaselineBytes,
    long DeltaBytes,
    DateTimeOffset BaselineCapturedAt);
