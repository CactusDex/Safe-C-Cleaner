using SafeCCleaner.Core.Interfaces;
using SafeCCleaner.Core.Models;
using SafeCCleaner.Core.Services;

namespace SafeCCleaner.Core.Rules;

public sealed class ThumbnailCacheCleanupRule : ICleanupRule
{
    public string Id => "thumbnail-cache";
    public string DisplayName => "Кэш миниатюр";
    public string Description => "Предпросмотры изображений и видео Проводника. Оригинальные файлы не затрагиваются; миниатюры будут созданы заново.";
    public CleanupRisk Risk => CleanupRisk.Safe;
    public bool RequiresElevation => false;
    public bool DefaultSelected => true;

    private static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft", "Windows", "Explorer");

    public Task<CleanupScanResult> ScanAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        long bytes = 0, files = 0;
        if (!Directory.Exists(Root))
            return new CleanupScanResult(Id, 0, 0);

        try
        {
            foreach (var file in Directory.EnumerateFiles(Root, "thumbcache_*.db", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    bytes += new FileInfo(file).Length;
                    files++;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return new CleanupScanResult(Id, bytes, files);
    }, cancellationToken);

    public async Task<CleanupResult> CleanAsync(CancellationToken cancellationToken = default)
    {
        var stats = await SafeDeletion.DeleteMatchingFilesAsync(Root, "thumbcache_*.db", cancellationToken);
        return new CleanupResult(Id, true, stats.FreedBytes, stats.DeletedFiles, stats.SkippedFiles,
            stats.SkippedFiles > 0 ? "Часть кэша используется Проводником и была пропущена." : null);
    }
}
