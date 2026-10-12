namespace SafeCCleaner.Core.Models;

public sealed record SystemOperationResult(
    string OperationId,
    bool Success,
    long FreedBytes,
    string Message,
    string? Details = null)
{
    public static SystemOperationResult Failed(string operationId, string message, string? details = null) =>
        new(operationId, false, 0, message, details);
}
