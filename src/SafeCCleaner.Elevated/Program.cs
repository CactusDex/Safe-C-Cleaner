using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using System.Text.RegularExpressions;
using SafeCCleaner.Core.Interfaces;
using SafeCCleaner.Core.Models;
using SafeCCleaner.Core.Rules;
using SafeCCleaner.Core.Services;

namespace SafeCCleaner.Elevated;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 3 || !Guid.TryParse(args[2], out var requestId))
            return 2;

        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
            return 4;

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "clean" => await RunCleanupRuleAsync(args[1], requestId),
                "system" => await RunSystemOperationAsync(args[1], requestId),
                _ => 2
            };
        }
        catch (Exception ex)
        {
            if (args[0].Equals("clean", StringComparison.OrdinalIgnoreCase))
                await WriteResultAsync(requestId, CleanupResult.Failed(args[1], ex.Message));
            else
                await WriteResultAsync(requestId, SystemOperationResult.Failed(args[1], ex.Message, ex.ToString()));
            return 20;
        }
    }

    private static async Task<int> RunCleanupRuleAsync(string ruleId, Guid requestId)
    {
        ICleanupRule? rule = ruleId.ToLowerInvariant() switch
        {
            "windows-temp" => new WindowsTempCleanupRule(),
            "system-wer" => new SystemWerCleanupRule(),
            "system-crash-dumps" => new SystemCrashDumpsCleanupRule(),
            _ => null
        };

        if (rule is null || !rule.RequiresElevation)
            return 5;

        CleanupResult result;
        try
        {
            result = await rule.CleanAsync();
        }
        catch (Exception ex)
        {
            result = CleanupResult.Failed(rule.Id, ex.Message);
        }

        await WriteResultAsync(requestId, result);
        return result.Success ? 0 : 10;
    }

    private static async Task<int> RunSystemOperationAsync(string operationId, Guid requestId)
    {
        var result = operationId.ToLowerInvariant() switch
        {
            "analyze-component-store" => await AnalyzeComponentStoreAsync(),
            "component-cleanup" => await CleanupComponentStoreAsync(),
            "windows-update-cleanup" => await CleanupWindowsUpdateAsync(),
            "windows-upgrade-leftovers" => await CleanupUpgradeLeftoversAsync(),
            "delivery-optimization" => await CleanupDeliveryOptimizationAsync(),
            "hibernate-off" => await SetHibernationAsync(false),
            "hibernate-on" => await SetHibernationAsync(true),
            _ => SystemOperationResult.Failed(operationId, "Операция не разрешена elevated helper.")
        };

        await WriteResultAsync(requestId, result);
        return result.Success ? 0 : 10;
    }

    private static async Task<SystemOperationResult> AnalyzeComponentStoreAsync()
    {
        var dism = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Dism.exe");
        var process = await RunProcessAsync(dism, "/English /Online /Cleanup-Image /AnalyzeComponentStore");
        if (process.ExitCode != 0)
            return SystemOperationResult.Failed("analyze-component-store", "DISM не смог проанализировать хранилище компонентов.", process.CombinedOutput);

        var recommended = process.CombinedOutput.Contains("Component Store Cleanup Recommended : Yes", StringComparison.OrdinalIgnoreCase);
        var message = recommended
            ? "Windows рекомендует очистку старых компонентов."
            : "Windows не считает очистку хранилища компонентов необходимой прямо сейчас.";

        var details = BuildComponentStoreSummary(process.CombinedOutput, recommended);
        return new SystemOperationResult("analyze-component-store", true, 0, message, details);
    }

    private static async Task<SystemOperationResult> CleanupComponentStoreAsync()
    {
        const string id = "component-cleanup";
        var before = GetSystemDriveFreeBytes();
        var dism = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Dism.exe");
        var process = await RunProcessAsync(dism, "/English /Online /Cleanup-Image /StartComponentCleanup");
        var after = GetSystemDriveFreeBytes();

        if (process.ExitCode != 0)
            return SystemOperationResult.Failed(id, "DISM не смог выполнить StartComponentCleanup.", process.CombinedOutput);

        return new SystemOperationResult(id, true, Math.Max(0, after - before),
            "Старые заменённые компоненты Windows очищены штатным DISM. /ResetBase не использовался.",
            TrimDetails(process.CombinedOutput));
    }

    private static async Task<SystemOperationResult> CleanupWindowsUpdateAsync()
    {
        const string id = "windows-update-cleanup";
        const string volumeCachesRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VolumeCaches";
        const string updateCleanupCategory = "Update Cleanup";
        const string stateFlagName = "StateFlags9133";
        const int stateFlagValue = 2;

        var cleanmgr = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cleanmgr.exe");
        if (!File.Exists(cleanmgr))
            return SystemOperationResult.Failed(id, "cleanmgr.exe не найден в этой версии Windows.");

        var savedFlags = new Dictionary<string, (bool Exists, object? Value, RegistryValueKind Kind)>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var root = baseKey.OpenSubKey(volumeCachesRoot, writable: true);
            if (root is null)
                return SystemOperationResult.Failed(id, "Конфигурация Windows Disk Cleanup не найдена.");

            var categoryNames = root.GetSubKeyNames();
            if (!categoryNames.Contains(updateCleanupCategory, StringComparer.OrdinalIgnoreCase))
                return SystemOperationResult.Failed(id,
                    "Компонент Windows Update Cleanup не найден в Disk Cleanup. Windows могла уже удалить эти данные или не поддерживает эту категорию.");

            // Isolate a dedicated profile. Existing StateFlags9133 values are saved and restored,
            // so /sagerun:9133 cannot accidentally execute unrelated cleanup categories.
            foreach (var categoryName in categoryNames)
            {
                using var category = root.OpenSubKey(categoryName, writable: true);
                if (category is null)
                    continue;

                var exists = category.GetValueNames().Contains(stateFlagName, StringComparer.OrdinalIgnoreCase);
                object? value = null;
                var kind = RegistryValueKind.None;
                if (exists)
                {
                    value = category.GetValue(stateFlagName);
                    kind = category.GetValueKind(stateFlagName);
                }

                savedFlags[categoryName] = (exists, value, kind);
                category.DeleteValue(stateFlagName, throwOnMissingValue: false);
            }

            using (var updateCategory = root.OpenSubKey(updateCleanupCategory, writable: true))
            {
                if (updateCategory is null)
                    return SystemOperationResult.Failed(id, "Не удалось открыть категорию Windows Update Cleanup.");
                updateCategory.SetValue(stateFlagName, stateFlagValue, RegistryValueKind.DWord);
            }

            var before = GetSystemDriveFreeBytes();
            var process = await RunProcessAsync(cleanmgr, "/sagerun:9133");
            var after = GetSystemDriveFreeBytes();

            if (process.ExitCode != 0)
                return SystemOperationResult.Failed(id,
                    "Windows Disk Cleanup не смог выполнить Windows Update Cleanup.", process.CombinedOutput);

            var freed = Math.Max(0, after - before);
            return new SystemOperationResult(id, true, freed,
                "Windows Update Cleanup выполнена штатным cleanmgr. Запускалась только категория Update Cleanup.",
                string.IsNullOrWhiteSpace(process.CombinedOutput)
                    ? "Временный профиль Disk Cleanup изолирован и восстанавливается после операции."
                    : TrimDetails(process.CombinedOutput));
        }
        catch (UnauthorizedAccessException ex)
        {
            return SystemOperationResult.Failed(id, "Нет доступа к системной конфигурации Disk Cleanup.", ex.Message);
        }
        catch (Exception ex)
        {
            return SystemOperationResult.Failed(id, "Не удалось выполнить Windows Update Cleanup.", ex.ToString());
        }
        finally
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var root = baseKey.OpenSubKey(volumeCachesRoot, writable: true);
                if (root is not null)
                {
                    foreach (var (categoryName, saved) in savedFlags)
                    {
                        using var category = root.OpenSubKey(categoryName, writable: true);
                        if (category is null)
                            continue;

                        category.DeleteValue(stateFlagName, throwOnMissingValue: false);
                        if (saved.Exists && saved.Value is not null)
                            category.SetValue(stateFlagName, saved.Value, saved.Kind);
                    }
                }
            }
            catch
            {
                // Do not mask the operation result. The profile is restored best-effort.
            }
        }
    }

    private static async Task<SystemOperationResult> CleanupUpgradeLeftoversAsync()
    {
        const string id = "windows-upgrade-leftovers";
        var before = GetSystemDriveFreeBytes();
        var cleanmgr = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cleanmgr.exe");
        if (!File.Exists(cleanmgr))
            return SystemOperationResult.Failed(id, "cleanmgr.exe не найден в Windows.");

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var drive = (Path.GetPathRoot(windows) ?? "C:\\").TrimEnd('\\');
        var process = await RunProcessAsync(cleanmgr, $"/AUTOCLEAN /D {drive}");
        var after = GetSystemDriveFreeBytes();
        if (process.ExitCode != 0)
            return SystemOperationResult.Failed(id, "Windows Disk Cleanup завершился с ошибкой.", process.CombinedOutput);

        return new SystemOperationResult(id, true, Math.Max(0, after - before),
            "Windows удалила остатки предыдущего обновления/установки через cleanmgr /AUTOCLEAN.",
            TrimDetails(process.CombinedOutput));
    }

    private static async Task<SystemOperationResult> CleanupDeliveryOptimizationAsync()
    {
        const string id = "delivery-optimization";
        var before = await MeasureDeliveryOptimizationAsync();
        var powershell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe");

        var command = "Import-Module DeliveryOptimization -ErrorAction Stop; Delete-DeliveryOptimizationCache -Force -ErrorAction Stop";
        var process = await RunProcessAsync(powershell,
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{command}\"");
        var after = await MeasureDeliveryOptimizationAsync();

        if (process.ExitCode != 0)
            return SystemOperationResult.Failed(id, "Не удалось очистить кэш Delivery Optimization.", process.CombinedOutput);

        return new SystemOperationResult(id, true, Math.Max(0, before - after),
            "Кэш Delivery Optimization очищен штатным PowerShell-модулем Windows. Закреплённые файлы не удалялись.",
            TrimDetails(process.CombinedOutput));
    }

    private static async Task<SystemOperationResult> SetHibernationAsync(bool enabled)
    {
        var id = enabled ? "hibernate-on" : "hibernate-off";
        var before = GetHiberfileBytes();
        var powercfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "powercfg.exe");
        var process = await RunProcessAsync(powercfg, enabled ? "/hibernate on" : "/hibernate off");
        var after = GetHiberfileBytes();

        if (process.ExitCode != 0)
            return SystemOperationResult.Failed(id, "powercfg не смог изменить состояние гибернации.", process.CombinedOutput);

        var freed = enabled ? 0 : Math.Max(0, before - after);
        return new SystemOperationResult(id, true, freed,
            enabled
                ? "Гибернация снова включена. Windows может заново создать hiberfil.sys."
                : "Гибернация отключена, hiberfil.sys освобождён. Быстрый запуск Windows также может измениться.",
            TrimDetails(process.CombinedOutput));
    }

    private static long GetSystemDriveFreeBytes()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var root = Path.GetPathRoot(windows) ?? "C:\\";
        return new DriveInfo(root).AvailableFreeSpace;
    }

    private static long GetHiberfileBytes()
    {
        try
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var root = Path.GetPathRoot(windows) ?? "C:\\";
            var path = Path.Combine(root, "hiberfil.sys");
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static async Task<long> MeasureDeliveryOptimizationAsync()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var cache = Path.Combine(windows, "ServiceProfiles", "NetworkService", "AppData", "Local",
            "Microsoft", "Windows", "DeliveryOptimization", "Cache");
        var sizer = new DirectorySizer();
        return (await sizer.MeasureAsync(cache)).Bytes;
    }

    private static async Task<ProcessResult> RunProcessAsync(string fileName, string arguments)
    {
        if (!File.Exists(fileName))
            return new ProcessResult(-1, string.Empty, $"Не найден файл: {fileName}");

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, await stdoutTask, await stderrTask);
    }

    private static string BuildComponentStoreSummary(string output, bool recommended)
    {
        static string? Extract(string source, string label)
        {
            var match = Regex.Match(
                source,
                "^" + Regex.Escape(label) + @"\s*:\s*(.+)$",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        var rows = new List<string>();
        void Add(string caption, string sourceLabel)
        {
            var value = Extract(output, sourceLabel);
            if (!string.IsNullOrWhiteSpace(value))
                rows.Add($"{caption}: {value}");
        }

        Add("Размер по Проводнику", "Windows Explorer Reported Size of Component Store");
        Add("Фактический размер", "Actual Size of Component Store");
        Add("Общие файлы Windows", "Shared with Windows");
        Add("Резервные/отключённые компоненты", "Backups and Disabled Features");
        Add("Кэш и временные данные", "Cache and Temporary Data");
        Add("Пакетов можно освободить", "Number of Reclaimable Packages");
        Add("Последняя очистка", "Date of Last Cleanup");
        rows.Add($"Рекомендация Windows: {(recommended ? "очистить" : "очистка не требуется")}");

        return string.Join(Environment.NewLine, rows);
    }

    private static string TrimDetails(string value)
    {
        const int max = 12000;
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        value = value.Trim();
        return value.Length <= max ? value : value[^max..];
    }

    private static string ResultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SafeCCleaner", "elevated-results");

    private static async Task WriteResultAsync<T>(Guid requestId, T result)
    {
        Directory.CreateDirectory(ResultDirectory);
        var resultPath = Path.Combine(ResultDirectory, requestId.ToString("N") + ".json");
        await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(result));
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string CombinedOutput => string.Join(Environment.NewLine,
            new[] { StandardOutput, StandardError }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}
