using SafeCCleaner.Core.Models;

namespace SafeCCleaner.Core.Rules;

public sealed class BrowserCacheCleanupRule : DirectoryCleanupRuleBase
{
    public override string Id => "browser-cache";
    public override string DisplayName => "Кэши браузеров";
    public override string Description => "Только Cache / Code Cache / GPUCache для Chrome, Edge и cache2 Firefox. Cookies и профиль не удаляются.";
    public override CleanupRisk Risk => CleanupRisk.Safe;
    public override bool RequiresElevation => false;
    public override bool DefaultSelected => true;

    protected override IEnumerable<string> GetRoots()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        foreach (var root in ChromiumCaches(Path.Combine(local, "Google", "Chrome", "User Data")))
            yield return root;
        foreach (var root in ChromiumCaches(Path.Combine(local, "Microsoft", "Edge", "User Data")))
            yield return root;

        var firefoxProfiles = Path.Combine(roaming, "Mozilla", "Firefox", "Profiles");
        if (Directory.Exists(firefoxProfiles))
        {
            foreach (var profile in SafeDirectories(firefoxProfiles))
            {
                var cache2 = Path.Combine(profile, "cache2");
                if (Directory.Exists(cache2))
                    yield return cache2;
            }
        }
    }

    private static IEnumerable<string> ChromiumCaches(string userData)
    {
        if (!Directory.Exists(userData))
            yield break;

        foreach (var profile in SafeDirectories(userData))
        {
            var name = Path.GetFileName(profile);
            if (!name.Equals("Default", StringComparison.OrdinalIgnoreCase) &&
                !name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase) &&
                !name.Equals("Guest Profile", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var relative in new[] { "Cache", "Code Cache", "GPUCache" })
            {
                var cache = Path.Combine(profile, relative);
                if (Directory.Exists(cache))
                    yield return cache;
            }
        }
    }

    private static IEnumerable<string> SafeDirectories(string root)
    {
        try
        {
            return Directory.EnumerateDirectories(root)
                .Where(path =>
                {
                    try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0; }
                    catch { return false; }
                })
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
