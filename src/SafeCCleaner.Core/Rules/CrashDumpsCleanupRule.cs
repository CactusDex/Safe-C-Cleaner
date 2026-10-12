using SafeCCleaner.Core.Models;

namespace SafeCCleaner.Core.Rules;

public sealed class CrashDumpsCleanupRule : DirectoryCleanupRuleBase
{
    public override string Id => "crash-dumps";
    public override string DisplayName => "Crash Dumps";
    public override string Description => "Локальные дампы падений приложений. Нужны в основном для диагностики сбоев.";
    public override CleanupRisk Risk => CleanupRisk.Advanced;
    public override bool RequiresElevation => false;
    public override bool DefaultSelected => false;

    protected override IEnumerable<string> GetRoots()
    {
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps");
    }
}
