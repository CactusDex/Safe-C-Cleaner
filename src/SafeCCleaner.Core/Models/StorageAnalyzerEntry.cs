namespace SafeCCleaner.Core.Models;

public sealed record StorageAnalyzerEntry(
    string Name,
    string Path,
    long Bytes,
    DateTimeOffset LastWriteTime,
    bool IsDirectory,
    string Category,
    bool AccessLimited = false);

public sealed record StorageAnalyzerResult(
    string RootPath,
    long ScannedBytes,
    long FileCount,
    long DirectoryCount,
    int AccessLimitedCount,
    TimeSpan Duration,
    IReadOnlyList<StorageAnalyzerEntry> LargestFiles,
    IReadOnlyList<StorageAnalyzerEntry> LargestDirectories);

public sealed record StorageAnalyzerProgress(
    string CurrentPath,
    long FilesScanned,
    long DirectoriesScanned,
    double ApproximatePercent,
    int AccessLimitedCount);
