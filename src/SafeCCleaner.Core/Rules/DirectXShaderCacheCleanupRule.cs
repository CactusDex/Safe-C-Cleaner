using SafeCCleaner.Core.Models;

namespace SafeCCleaner.Core.Rules;

public sealed class DirectXShaderCacheCleanupRule : DirectoryCleanupRuleBase
{
    public override string Id => "directx-shader-cache";
    public override string DisplayName => "DirectX Shader Cache";
    public override string Description => "Кэш скомпилированных шейдеров DirectX. При необходимости Windows/игры создадут его заново.";
    public override CleanupRisk Risk => CleanupRisk.Safe;
    public override bool RequiresElevation => false;
    public override bool DefaultSelected => true;

    protected override IEnumerable<string> GetRoots()
    {
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache");
    }
}
