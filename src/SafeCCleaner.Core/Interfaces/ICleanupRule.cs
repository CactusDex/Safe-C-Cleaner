using SafeCCleaner.Core.Models;

namespace SafeCCleaner.Core.Interfaces;

public interface ICleanupRule
{
    string Id { get; }
    string DisplayName { get; }
    string Description { get; }
    CleanupRisk Risk { get; }
    bool RequiresElevation { get; }
    bool DefaultSelected { get; }

    Task<CleanupScanResult> ScanAsync(CancellationToken cancellationToken = default);
    Task<CleanupResult> CleanAsync(CancellationToken cancellationToken = default);
}
