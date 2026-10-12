using SafeCCleaner.Core.Interfaces;
using SafeCCleaner.Core.Models;
using SafeCCleaner.Core.Services;

namespace SafeCCleaner.Core.Rules;

public abstract class AgedDirectoryCleanupRuleBase : ICleanupRule
{
    private readonly DirectorySizer _sizer = new();

    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
    public abstract CleanupRisk Risk { get; }
    public abstract bool RequiresElevation { get; }
    public abstract bool DefaultSelected { get; }

    protected virtual TimeSpan MinimumAge => TimeSpan.FromDays(7);
    protected abstract IEnumerable<string> GetRoots();

    public virtual async Task<CleanupScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - MinimumAge;
        long bytes = 0, files = 0;

        foreach (var root in GetRoots().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var measured = await _sizer.MeasureOlderThanAsync(root, cutoff, cancellationToken);
            bytes += measured.Bytes;
            files += measured.Files;
        }

        return new CleanupScanResult(Id, bytes, files, "SAFE учитывает только временные файлы, не изменявшиеся более 7 дней.");
    }

    public virtual async Task<CleanupResult> CleanAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - MinimumAge;
        long bytes = 0, deleted = 0, skipped = 0;

        foreach (var root in GetRoots().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var stats = await SafeDeletion.DeleteContentsOlderThanAsync(root, cutoff, cancellationToken);
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
            skipped > 0 ? $"Пропущено занятых или недоступных объектов: {skipped}." : null);
    }
}
