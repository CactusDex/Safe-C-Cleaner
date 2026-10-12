namespace SafeCCleaner.Core.Models;

public sealed record DeepCleanSnapshot(
    long WindowsOldBytes,
    long HiberfileBytes,
    long DeliveryOptimizationBytes,
    bool HibernationEnabled);
