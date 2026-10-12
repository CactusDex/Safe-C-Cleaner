namespace SafeCCleaner.Core.Models;

public sealed record CleanupResult(
    string RuleId,
    bool Success,
    long FreedBytes,
    long DeletedFiles,
    long SkippedFiles,
    string? Message = null)
{
    public static CleanupResult Failed(string ruleId, string message) =>
        new(ruleId, false, 0, 0, 0, message);
}
