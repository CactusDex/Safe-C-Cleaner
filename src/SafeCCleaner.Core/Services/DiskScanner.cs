using SafeCCleaner.Core.Models;

namespace SafeCCleaner.Core.Services;

public sealed class DiskScanner
{
    private readonly DirectorySizer _sizer = new();

    public async Task<DiskSnapshot> ScanAsync(
        string driveRoot = "C:\\",
        IProgress<DiskScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var drive = new DriveInfo(driveRoot);
        var folders = new List<FolderUsage>();

        var targets = GetTrackedFolders();
        for (var index = 0; index < targets.Count; index++)
        {
            var target = targets[index];
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new DiskScanProgress(index, targets.Count, target.DisplayName));
            var measured = await _sizer.MeasureAsync(target.Path, cancellationToken);
            folders.Add(new FolderUsage(target.Key, target.DisplayName, target.Path, measured.Bytes));
            progress?.Report(new DiskScanProgress(index + 1, targets.Count, target.DisplayName));
        }

        return new DiskSnapshot(
            DateTimeOffset.UtcNow,
            drive.Name,
            drive.TotalSize,
            drive.AvailableFreeSpace,
            folders.OrderByDescending(x => x.Bytes).ToArray());
    }

    private static IReadOnlyList<(string Key, string DisplayName, string Path)> GetTrackedFolders()
    {
        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        var result = new List<(string, string, string)>
        {
            ("windows", "Windows", windows),
            ("program-files", "Program Files", programFiles),
            ("program-files-x86", "Program Files (x86)", programFilesX86),
            ("program-data", "ProgramData", programData),
            ("user-profile", "Профиль пользователя", user),
            ("appdata-local", "AppData Local", local),
            ("downloads", "Downloads", Path.Combine(user, "Downloads")),
            ("desktop", "Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)),
            ("gradle", "Gradle cache", Path.Combine(user, ".gradle")),
            ("nuget", "NuGet cache", Path.Combine(user, ".nuget", "packages")),
            ("npm", "npm cache", Path.Combine(local, "npm-cache")),
            ("android-sdk", "Android SDK", Path.Combine(local, "Android", "Sdk")),
            ("android-avd", "Android AVD", Path.Combine(user, ".android", "avd")),
            ("docker", "Docker Desktop data", Path.Combine(local, "Docker"))
        };

        return result
            .Where(x => !string.IsNullOrWhiteSpace(x.Item3))
            .DistinctBy(x => x.Item3, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public sealed record DiskScanProgress(int Completed, int Total, string DisplayName);
