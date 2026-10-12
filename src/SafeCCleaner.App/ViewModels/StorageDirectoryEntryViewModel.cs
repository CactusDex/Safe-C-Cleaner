using SafeCCleaner.Core.Models;
using SafeCCleaner.Core.Services;

namespace SafeCCleaner.App.ViewModels;

public sealed class StorageDirectoryEntryViewModel
{
    public StorageDirectoryEntryViewModel(StorageDirectoryEntry entry)
    {
        Name = entry.Name;
        Path = entry.Path;
        Bytes = entry.Bytes;
        FileCount = entry.FileCount;
        DirectoryCount = entry.DirectoryCount;
        IsDirectory = entry.IsDirectory;
        AccessLimited = entry.AccessLimited;
    }

    public string Name { get; }
    public string Path { get; }
    public long Bytes { get; }
    public long FileCount { get; }
    public long DirectoryCount { get; }
    public bool IsDirectory { get; }
    public bool AccessLimited { get; }
    public string SizeText => ByteSizeFormatter.Format(Bytes);
    public string DetailText => IsDirectory
        ? $"{FileCount:N0} файлов · {DirectoryCount:N0} папок" + (AccessLimited ? " · частично недоступно" : string.Empty)
        : AccessLimited ? "нет доступа" : "файл";
}
