namespace SafeCCleaner.Core.Models;

public sealed record CleanupHistoryRecord(
    long Id,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string OperationName,
    string Mode,
    bool Success,
    long BeforeFreeBytes,
    long AfterFreeBytes,
    long ReportedFreedBytes,
    long SkippedFiles,
    string? Summary,
    IReadOnlyList<CleanupHistoryItem> Items)
{
    public long ObservedFreedBytes => Math.Max(0, AfterFreeBytes - BeforeFreeBytes);
}

public sealed record CleanupHistoryItem(
    string RuleId,
    string DisplayName,
    CleanupRisk Risk,
    bool Success,
    long FreedBytes,
    long DeletedFiles,
    long SkippedFiles,
    string? Message);
