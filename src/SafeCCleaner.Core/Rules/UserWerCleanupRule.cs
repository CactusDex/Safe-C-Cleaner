using SafeCCleaner.Core.Models;

namespace SafeCCleaner.Core.Rules;

public sealed class UserWerCleanupRule : DirectoryCleanupRuleBase
{
    public override string Id => "user-wer";
    public override string DisplayName => "Отчёты об ошибках Windows";
    public override string Description => "Архив и очередь Windows Error Reporting для текущего пользователя. Они нужны только для диагностики старых сбоев.";
    public override CleanupRisk Risk => CleanupRisk.Safe;
    public override bool RequiresElevation => false;
    public override bool DefaultSelected => true;

    protected override IEnumerable<string> GetRoots()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "WER");
        yield return Path.Combine(root, "ReportArchive");
        yield return Path.Combine(root, "ReportQueue");
        yield return Path.Combine(root, "Temp");
    }
}
