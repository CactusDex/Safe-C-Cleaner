using System.Diagnostics;
using System.Text.Json;
using SafeCCleaner.Core.Models;

namespace SafeCCleaner.App.Services;

public sealed class ElevatedRunner
{
    private static readonly HashSet<string> AllowedCleanupRules = new(StringComparer.OrdinalIgnoreCase)
    {
        "windows-temp",
        "system-wer",
        "system-crash-dumps"
    };

    private static readonly HashSet<string> AllowedSystemOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "analyze-component-store",
        "component-cleanup",
        "windows-update-cleanup",
        "windows-upgrade-leftovers",
        "delivery-optimization",
        "hibernate-off",
        "hibernate-on"
    };

    public async Task<CleanupResult> RunCleanupAsync(string ruleId, CancellationToken cancellationToken = default)
    {
        if (!AllowedCleanupRules.Contains(ruleId))
            return CleanupResult.Failed(ruleId, "Операция не разрешена elevated helper.");

        var response = await RunHelperAsync("clean", ruleId, cancellationToken);
        if (!response.Success)
            return CleanupResult.Failed(ruleId, response.ErrorMessage ?? "Неизвестная ошибка UAC helper.");

        try
        {
            return JsonSerializer.Deserialize<CleanupResult>(response.Json!)
                ?? CleanupResult.Failed(ruleId, "Некорректный ответ helper.");
        }
        catch (JsonException ex)
        {
            return CleanupResult.Failed(ruleId, "Некорректный JSON от helper: " + ex.Message);
        }
    }

    public async Task<SystemOperationResult> RunSystemOperationAsync(string operationId, CancellationToken cancellationToken = default)
    {
        if (!AllowedSystemOperations.Contains(operationId))
            return SystemOperationResult.Failed(operationId, "Операция не разрешена elevated helper.");

        var response = await RunHelperAsync("system", operationId, cancellationToken);
        if (!response.Success)
            return SystemOperationResult.Failed(operationId, response.ErrorMessage ?? "Неизвестная ошибка UAC helper.");

        try
        {
            return JsonSerializer.Deserialize<SystemOperationResult>(response.Json!)
                ?? SystemOperationResult.Failed(operationId, "Некорректный ответ helper.");
        }
        catch (JsonException ex)
        {
            return SystemOperationResult.Failed(operationId, "Некорректный JSON от helper: " + ex.Message);
        }
    }

    private static async Task<HelperResponse> RunHelperAsync(
        string mode,
        string operationId,
        CancellationToken cancellationToken)
    {
        var helper = Path.Combine(AppContext.BaseDirectory, "SafeCCleaner.Elevated.exe");
        if (!File.Exists(helper))
            return new HelperResponse(false, null, "SafeCCleaner.Elevated.exe не найден рядом с приложением.");

        var requestId = Guid.NewGuid();
        var resultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SafeCCleaner", "elevated-results", requestId.ToString("N") + ".json");

        try
        {
            if (File.Exists(resultPath))
                File.Delete(resultPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = helper,
                Arguments = $"{mode} {operationId} {requestId:D}",
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            };

            using var process = Process.Start(startInfo);
            if (process is null)
                return new HelperResponse(false, null, "Не удалось запустить UAC helper.");

            await process.WaitForExitAsync(cancellationToken);
            if (!File.Exists(resultPath))
                return new HelperResponse(false, null, "Helper завершился без результата. Возможно, UAC был отменён.");

            var json = await File.ReadAllTextAsync(resultPath, cancellationToken);
            File.Delete(resultPath);
            return new HelperResponse(true, json, null);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new HelperResponse(false, null, "Запрос UAC отменён пользователем.");
        }
        catch (Exception ex)
        {
            return new HelperResponse(false, null, ex.Message);
        }
    }

    private sealed record HelperResponse(bool Success, string? Json, string? ErrorMessage);
}
