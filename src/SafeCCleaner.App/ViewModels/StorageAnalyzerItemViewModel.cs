using SafeCCleaner.Core.Models;
using SafeCCleaner.Core.Services;

namespace SafeCCleaner.App.ViewModels;

public sealed class StorageAnalyzerItemViewModel
{
    public StorageAnalyzerItemViewModel(StorageAnalyzerEntry entry)
    {
        Entry = entry;
    }

    public StorageAnalyzerEntry Entry { get; }
    public string Name => Entry.Name;
    public string Path => Entry.Path;
    public long Bytes => Entry.Bytes;
    public string SizeText => ByteSizeFormatter.Format(Entry.Bytes);
    public bool IsDirectory => Entry.IsDirectory;
    public string Category => Entry.Category;
    public DateTimeOffset LastWriteTime => Entry.LastWriteTime;
    public string ModifiedText => Entry.LastWriteTime == DateTimeOffset.MinValue
        ? "дата неизвестна"
        : Entry.LastWriteTime.ToLocalTime().ToString("dd.MM.yyyy");
    public string AccessText => Entry.AccessLimited ? "частичный доступ" : string.Empty;
}
