using SafeCCleaner.Core.Models;

namespace SafeCCleaner.Core.Rules;

public sealed class UserTempCleanupRule : AgedDirectoryCleanupRuleBase
{
    public override string Id => "user-temp";
    public override string DisplayName => "Временные файлы пользователя";
    public override string Description => "%LOCALAPPDATA%\\Temp. SAFE удаляет только файлы старше 7 дней; свежие и занятые файлы остаются.";
    public override CleanupRisk Risk => CleanupRisk.Safe;
    public override bool RequiresElevation => false;
    public override bool DefaultSelected => true;

    protected override IEnumerable<string> GetRoots()
    {
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp");
    }
}
