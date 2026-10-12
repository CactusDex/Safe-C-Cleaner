using System.Text.Json;

namespace SafeCCleaner.App.Services;

internal sealed class AppSettings
{
    public bool AutoScanOnStartup { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public int ScannerCacheMinutes { get; set; } = 15;
    public bool IsNavigationPaneOpen { get; set; } = true;
    public string LastSection { get; set; } = "overview";
    public int WindowWidth { get; set; } = 1240;
    public int WindowHeight { get; set; } = 850;
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
}

internal sealed class AppSettingsService
{
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SafeCCleaner");

    public static string SettingsPath => Path.Combine(DirectoryPath, "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath))
                ?? new AppSettings();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Could not load application settings.", ex);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Could not save application settings.", ex);
        }
    }

    public void Reset()
    {
        try
        {
            if (File.Exists(SettingsPath))
                File.Delete(SettingsPath);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Could not reset application settings.", ex);
        }
    }
}
