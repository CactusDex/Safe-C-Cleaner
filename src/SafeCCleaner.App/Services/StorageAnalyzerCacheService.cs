using System.Text.Json;
using SafeCCleaner.Core.Models;

namespace SafeCCleaner.App.Services;

internal sealed class StorageAnalyzerCacheService
{
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SafeCCleaner");

    private static readonly string CachePath = Path.Combine(DirectoryPath, "storage-analyzer-last.json");

    public StorageAnalyzerCacheData? Load()
    {
        try
        {
            if (!File.Exists(CachePath))
                return null;

            return JsonSerializer.Deserialize<StorageAnalyzerCacheData>(File.ReadAllText(CachePath));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Could not load Storage Analyzer cache.", ex);
            return null;
        }
    }

    public void Save(StorageAnalyzerResult result, DateTimeOffset capturedAt)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var cache = new StorageAnalyzerCacheData
            {
                CapturedAt = capturedAt,
                RootPath = result.RootPath,
                ScannedBytes = result.ScannedBytes,
                FileCount = result.FileCount,
                DirectoryCount = result.DirectoryCount,
                AccessLimitedCount = result.AccessLimitedCount,
                DurationTicks = result.Duration.Ticks,
                LargestFiles = result.LargestFiles.ToList(),
                LargestDirectories = result.LargestDirectories.ToList()
            };

            var tempPath = CachePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = false }));
            File.Move(tempPath, CachePath, true);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Could not save Storage Analyzer cache.", ex);
        }
    }
}

internal sealed class StorageAnalyzerCacheData
{
    public DateTimeOffset CapturedAt { get; set; }
    public string RootPath { get; set; } = @"C:\";
    public long ScannedBytes { get; set; }
    public long FileCount { get; set; }
    public long DirectoryCount { get; set; }
    public int AccessLimitedCount { get; set; }
    public long DurationTicks { get; set; }
    public List<StorageAnalyzerEntry> LargestFiles { get; set; } = [];
    public List<StorageAnalyzerEntry> LargestDirectories { get; set; } = [];

    public StorageAnalyzerResult ToResult() => new(
        RootPath,
        ScannedBytes,
        FileCount,
        DirectoryCount,
        AccessLimitedCount,
        TimeSpan.FromTicks(Math.Max(0, DurationTicks)),
        LargestFiles,
        LargestDirectories);
}
