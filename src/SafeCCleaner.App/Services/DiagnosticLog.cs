using System.Diagnostics;

namespace SafeCCleaner.App.Services;

internal static class DiagnosticLog
{
    public static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SafeCCleaner");

    public static string LogPath => Path.Combine(DirectoryPath, "startup.log");

    public static void Write(string message, Exception? exception = null)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var text = $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}";
            if (exception is not null)
            {
                text += exception + Environment.NewLine;
                text += $"HResult: 0x{exception.HResult:X8}{Environment.NewLine}";
                if (exception.Data.Count > 0)
                {
                    text += "Exception.Data:" + Environment.NewLine;
                    foreach (System.Collections.DictionaryEntry entry in exception.Data)
                        text += $"  {entry.Key}: {entry.Value}{Environment.NewLine}";
                }
            }
            File.AppendAllText(LogPath, text + Environment.NewLine);
        }
        catch
        {
            // Diagnostics must never make startup fail.
        }
    }

    public static void OpenInNotepad()
    {
        try
        {
            if (!File.Exists(LogPath))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{LogPath}\"",
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }
}
