using SafeCCleaner.Core.Models;
using SafeCCleaner.Core.Services;

namespace SafeCCleaner.App.ViewModels;

public sealed class StorageGrowthViewModel
{
    public StorageGrowthViewModel(StorageGrowth growth)
    {
        Key = growth.Key;
        Name = growth.DisplayName;
        Path = growth.Path;
        CurrentBytes = growth.CurrentBytes;
        DeltaBytes = growth.DeltaBytes;
        BaselineCapturedAt = growth.BaselineCapturedAt;
    }

    public string Key { get; }
    public string Name { get; }
    public string Path { get; }
    public long CurrentBytes { get; }
    public long DeltaBytes { get; }
    public DateTimeOffset BaselineCapturedAt { get; }
    public string SizeText => ByteSizeFormatter.Format(CurrentBytes);
    public string DeltaText => DeltaBytes switch
    {
        > 0 => "+" + ByteSizeFormatter.Format(DeltaBytes),
        < 0 => "−" + ByteSizeFormatter.Format(Math.Abs(DeltaBytes)),
        _ => "без изменений"
    };
    public string PeriodText => $"с {BaselineCapturedAt.LocalDateTime:dd.MM HH:mm}";
    public bool HasGrowth => DeltaBytes > 0;
}
