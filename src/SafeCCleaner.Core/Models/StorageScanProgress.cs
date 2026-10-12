namespace SafeCCleaner.Core.Models;

public sealed record StorageScanProgress(
    string CurrentName,
    int CompletedItems,
    int TotalItems,
    int AccessLimitedItems)
{
    public double Percent => TotalItems <= 0 ? 0 : CompletedItems * 100d / TotalItems;
}
