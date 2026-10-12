using SafeCCleaner.Core.Interfaces;
using SafeCCleaner.Core.Rules;

namespace SafeCCleaner.Core.Services;

public static class CleanupRuleCatalog
{
    public static IReadOnlyList<ICleanupRule> CreateDefault() =>
    [
        new UserTempCleanupRule(),
        new WindowsTempCleanupRule(),
        new DirectXShaderCacheCleanupRule(),
        new GpuShaderCacheCleanupRule(),
        new ThumbnailCacheCleanupRule(),
        new BrowserCacheCleanupRule(),
        new UserWerCleanupRule(),
        new SystemWerCleanupRule(),
        new CrashDumpsCleanupRule(),
        new SystemCrashDumpsCleanupRule(),
        new RecycleBinCleanupRule()
    ];
}
