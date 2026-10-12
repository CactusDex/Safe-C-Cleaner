using SafeCCleaner.Core.Models;

namespace SafeCCleaner.Core.Rules;

public sealed class WindowsTempCleanupRule : AgedDirectoryCleanupRuleBase
{
    public override string Id => "windows-temp";
    public override string DisplayName => "Windows Temp";
    public override string Description => "C:\\Windows\\Temp. Через UAC удаляются только временные файлы старше 7 дней; свежие и занятые остаются.";
    public override CleanupRisk Risk => CleanupRisk.Safe;
    public override bool RequiresElevation => true;
    public override bool DefaultSelected => true;

    protected override IEnumerable<string> GetRoots()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        yield return Path.Combine(windows, "Temp");
    }
}
