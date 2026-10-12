using SafeCCleaner.Core.Models;
using SafeCCleaner.Core.Services;

namespace SafeCCleaner.App.ViewModels;

public sealed class FolderUsageViewModel
{
    private static readonly HashSet<string> OpenableKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "user-profile", "appdata-local", "downloads", "desktop",
        "gradle", "nuget", "npm", "android-sdk", "android-avd", "docker"
    };

    public FolderUsageViewModel(FolderUsage current, long? previousBytes)
    {
        Key = current.Key;
        Name = current.DisplayName;
        Path = current.Path;
        Bytes = current.Bytes;
        GrowthBytes = previousBytes.HasValue ? current.Bytes - previousBytes.Value : null;
    }

    public string Key { get; }
    public string Name { get; }
    public string Path { get; }
    public long Bytes { get; }
    public long? GrowthBytes { get; }
    public bool CanOpenLocation => OpenableKeys.Contains(Key) && Directory.Exists(Path);
    public string SizeText => ByteSizeFormatter.Format(Bytes);
    public string GrowthText => GrowthBytes switch
    {
        null => "первый снимок",
        > 0 => "+" + ByteSizeFormatter.Format(GrowthBytes.Value),
        < 0 => "−" + ByteSizeFormatter.Format(Math.Abs(GrowthBytes.Value)),
        _ => "без изменений"
    };
}
