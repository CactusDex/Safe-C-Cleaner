using SafeCCleaner.Core.Interfaces;
using SafeCCleaner.Core.Models;
using SafeCCleaner.Core.Services;

namespace SafeCCleaner.Core.Rules;

public abstract class DirectoryCleanupRuleBase : ICleanupRule
{
    private readonly DirectorySizer _sizer = new();

    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
    public abstract CleanupRisk Risk { get; }
    public abstract bool RequiresElevation { get; }
    public abstract bool DefaultSelected { get; }

    protected abstract IEnumerable<string> GetRoots();

    public virtual async Task<CleanupScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        long bytes = 0, files = 0;
        foreach (var root in GetRoots().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var measured = await _sizer.MeasureAsync(root, cancellationToken);
            bytes += measured.Bytes;
            files += measured.Files;
        }
        return new CleanupScanResult(Id, bytes, files);
    }

    public virtual async Task<CleanupResult> CleanAsync(CancellationToken cancellationToken = default)
    {
        long bytes = 0, deleted = 0, skipped = 0;
        foreach (var root in GetRoots().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var stats = await SafeDeletion.DeleteContentsAsync(root, cancellationToken);
            bytes += stats.FreedBytes;
            deleted += stats.DeletedFiles;
            skipped += stats.SkippedFiles;
        }
        return new CleanupResult(Id, true, bytes, deleted, skipped,
            skipped > 0 ? $"Пропущено занятых/недоступных объектов: {skipped}." : null);
    }
}
