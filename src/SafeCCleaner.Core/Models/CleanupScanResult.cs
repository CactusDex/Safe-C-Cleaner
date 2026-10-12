namespace SafeCCleaner.Core.Models;

public sealed record CleanupScanResult(
    string RuleId,
    long ReclaimableBytes,
    long FileCount,
    string? Warning = null);
