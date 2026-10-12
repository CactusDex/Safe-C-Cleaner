using SafeCCleaner.Core.Models;

namespace SafeCCleaner.Core.Rules;

/// <summary>
/// User-scoped GPU shader caches. These are generated caches only; games/drivers recreate them as needed.
/// System-wide driver folders are intentionally excluded.
/// </summary>
public sealed class GpuShaderCacheCleanupRule : DirectoryCleanupRuleBase
{
    public override string Id => "gpu-shader-cache";
    public override string DisplayName => "GPU Shader Cache";
    public override string Description => "Пользовательские кэши шейдеров NVIDIA/AMD. Игры и драйвер создадут их заново; системные папки драйверов не затрагиваются.";
    public override CleanupRisk Risk => CleanupRisk.Safe;
    public override bool RequiresElevation => false;
    public override bool DefaultSelected => true;

    protected override IEnumerable<string> GetRoots()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // NVIDIA user caches.
        yield return Path.Combine(local, "NVIDIA", "DXCache");
        yield return Path.Combine(local, "NVIDIA", "GLCache");
        yield return Path.Combine(local, "NVIDIA Corporation", "NV_Cache");

        // AMD user caches. Do not touch ProgramData or driver installation stores.
        yield return Path.Combine(local, "AMD", "DxCache");
        yield return Path.Combine(local, "AMD", "GLCache");
        yield return Path.Combine(local, "AMD", "VkCache");
    }
}
