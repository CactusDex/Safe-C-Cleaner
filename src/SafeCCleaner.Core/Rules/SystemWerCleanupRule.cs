using SafeCCleaner.Core.Models;

namespace SafeCCleaner.Core.Rules;

public sealed class SystemWerCleanupRule : DirectoryCleanupRuleBase
{
    public override string Id => "system-wer";
    public override string DisplayName => "Системные отчёты об ошибках";
    public override string Description => "Старые системные отчёты Windows Error Reporting. Очистка выполняется через отдельный UAC helper.";
    public override CleanupRisk Risk => CleanupRisk.Safe;
    public override bool RequiresElevation => true;
    public override bool DefaultSelected => true;

    protected override IEnumerable<string> GetRoots()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Microsoft", "Windows", "WER");
        yield return Path.Combine(root, "ReportArchive");
        yield return Path.Combine(root, "ReportQueue");
        yield return Path.Combine(root, "Temp");
    }
}
