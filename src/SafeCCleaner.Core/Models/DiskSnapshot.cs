namespace SafeCCleaner.Core.Models;

public sealed record DiskSnapshot(
    DateTimeOffset CapturedAt,
    string DriveRoot,
    long TotalBytes,
    long FreeBytes,
    IReadOnlyList<FolderUsage> Folders);
