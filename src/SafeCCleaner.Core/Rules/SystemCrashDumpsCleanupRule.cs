using SafeCCleaner.Core.Interfaces;
using SafeCCleaner.Core.Models;
using SafeCCleaner.Core.Services;

namespace SafeCCleaner.Core.Rules;

public sealed class SystemCrashDumpsCleanupRule : ICleanupRule
{
    private readonly DirectorySizer _sizer = new();

    public string Id => "system-crash-dumps";
    public string DisplayName => "Системные дампы Windows";
    public string Description => "MEMORY.DMP и %SystemRoot%\\Minidump после BSOD/критических сбоев. Полезны для диагностики причин падения Windows.";
    public CleanupRisk Risk => CleanupRisk.Advanced;
    public bool RequiresElevation => true;
    public bool DefaultSelected => false;

    public async Task<CleanupScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var memoryDump = Path.Combine(windows, "MEMORY.DMP");
        var minidump = Path.Combine(windows, "Minidump");

        long bytes = 0;
        long files = 0;

        try
        {
            if (File.Exists(memoryDump))
            {
                bytes += new FileInfo(memoryDump).Length;
                files++;
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }

        var mini = await _sizer.MeasureAsync(minidump, cancellationToken);
        bytes += mini.Bytes;
        files += mini.Files;

        return new CleanupScanResult(
            Id,
            bytes,
            files,
            "После удаления эти дампы нельзя будет использовать для анализа BSOD и критических сбоев Windows.");
    }

    public async Task<CleanupResult> CleanAsync(CancellationToken cancellationToken = default)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var memoryDump = Path.Combine(windows, "MEMORY.DMP");
        var minidump = Path.Combine(windows, "Minidump");

        long bytes = 0;
        long deleted = 0;
        long skipped = 0;

        try
        {
            if (File.Exists(memoryDump))
            {
                var attributes = File.GetAttributes(memoryDump);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    skipped++;
                }
                else
                {
                    long length = 0;
                    try { length = new FileInfo(memoryDump).Length; } catch { }
                    if ((attributes & FileAttributes.ReadOnly) != 0)
                        File.SetAttributes(memoryDump, attributes & ~FileAttributes.ReadOnly);
                    File.Delete(memoryDump);
                    bytes += length;
                    deleted++;
                }
            }
        }
        catch (UnauthorizedAccessException) { skipped++; }
        catch (IOException) { skipped++; }

        if (Directory.Exists(minidump))
        {
            var stats = await SafeDeletion.DeleteContentsAsync(minidump, cancellationToken);
            bytes += stats.FreedBytes;
            deleted += stats.DeletedFiles;
            skipped += stats.SkippedFiles;
        }

        return new CleanupResult(
            Id,
            true,
            bytes,
            deleted,
            skipped,
            skipped > 0 ? $"Пропущено занятых/недоступных дампов: {skipped}." : null);
    }
}
