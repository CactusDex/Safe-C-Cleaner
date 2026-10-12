namespace SafeCCleaner.Core.Models;

public sealed record StorageDirectoryEntry(
    string Name,
    string Path,
    long Bytes,
    long FileCount,
    long DirectoryCount,
    bool IsDirectory,
    bool AccessLimited = false);
