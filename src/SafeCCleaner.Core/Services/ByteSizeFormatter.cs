using System.Globalization;

namespace SafeCCleaner.Core.Services;

public static class ByteSizeFormatter
{
    public static string Format(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = Math.Max(0, bytes);
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return value.ToString(value >= 100 || unit == 0 ? "0" : "0.0", CultureInfo.InvariantCulture) + " " + units[unit];
    }
}
