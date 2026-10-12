namespace SafeCCleaner.Core.Models;

public sealed record FolderUsage(
    string Key,
    string DisplayName,
    string Path,
    long Bytes);
