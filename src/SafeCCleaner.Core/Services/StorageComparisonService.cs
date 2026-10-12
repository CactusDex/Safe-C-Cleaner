using SafeCCleaner.Core.Models;

namespace SafeCCleaner.Core.Services;

public sealed class StorageComparisonService
{
    public IReadOnlyList<StorageGrowth> Compare(DiskSnapshot current, DiskSnapshot? baseline)
    {
        if (baseline is null)
            return [];

        var old = baseline.Folders.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        return current.Folders
            .Where(x => old.ContainsKey(x.Key))
            .Select(x =>
            {
                var previous = old[x.Key];
                return new StorageGrowth(
                    x.Key,
                    x.DisplayName,
                    x.Path,
                    x.Bytes,
                    previous.Bytes,
                    x.Bytes - previous.Bytes,
                    baseline.CapturedAt);
            })
            .OrderByDescending(x => x.DeltaBytes)
            .ToArray();
    }

    public string BuildSummary(DiskSnapshot current, DiskSnapshot? baseline, IReadOnlyList<StorageGrowth> growth)
    {
        if (baseline is null)
            return "Первый снимок создан. После следующего снимка появится объяснение роста диска.";

        var currentUsed = current.TotalBytes - current.FreeBytes;
        var baselineUsed = baseline.TotalBytes - baseline.FreeBytes;
        var delta = currentUsed - baselineUsed;
        var positive = growth.Where(x => x.DeltaBytes > 0).Take(3).ToArray();

        if (delta <= 0 && positive.Length == 0)
            return $"Свободного места не стало меньше относительно {baseline.CapturedAt.LocalDateTime:dd.MM HH:mm}. Существенного роста отслеживаемых зон нет.";

        var deltaText = delta > 0
            ? $"занято дополнительно {ByteSizeFormatter.Format(delta)}"
            : $"свободного места стало больше на {ByteSizeFormatter.Format(Math.Abs(delta))}";

        if (positive.Length == 0)
            return $"С {baseline.CapturedAt.LocalDateTime:dd.MM HH:mm} {deltaText}, но рост находится вне отслеживаемых зон.";

        var causes = string.Join(", ", positive.Select(x => $"{x.DisplayName} +{ByteSizeFormatter.Format(x.DeltaBytes)}"));
        return $"С {baseline.CapturedAt.LocalDateTime:dd.MM HH:mm} {deltaText}. Основной рост: {causes}.";
    }
}
