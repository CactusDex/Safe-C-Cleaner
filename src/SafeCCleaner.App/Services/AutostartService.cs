using Microsoft.Win32;

namespace SafeCCleaner.App.Services;

internal sealed class AutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SafeCCleaner";

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Could not read autostart setting.", ex);
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Не удалось открыть HKCU Run.");

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
            throw new InvalidOperationException("Не удалось определить путь к SafeCCleaner.exe.");

        if (string.Equals(Path.GetFileName(executable), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Автозапуск доступен для собранного SafeCCleaner.exe, а не запуска через dotnet.exe.");

        key.SetValue(ValueName, $"\"{executable}\" --background", RegistryValueKind.String);
    }
}
