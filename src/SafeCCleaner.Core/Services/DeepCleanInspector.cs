using SafeCCleaner.Core.Models;

namespace SafeCCleaner.Core.Services;

public sealed class DeepCleanInspector
{
    private readonly DirectorySizer _sizer = new();

    public async Task<DeepCleanSnapshot> ScanAsync(CancellationToken cancellationToken = default)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var systemDrive = Path.GetPathRoot(windows) ?? "C:\\";

        var windowsOld = Path.Combine(systemDrive, "Windows.old");
        var deliveryOptimization = Path.Combine(
            windows,
            "ServiceProfiles",
            "NetworkService",
            "AppData",
            "Local",
            "Microsoft",
            "Windows",
            "DeliveryOptimization",
            "Cache");

        var oldMeasureTask = _sizer.MeasureAsync(windowsOld, cancellationToken);
        var doMeasureTask = _sizer.MeasureAsync(deliveryOptimization, cancellationToken);
        await Task.WhenAll(oldMeasureTask, doMeasureTask);

        var hiberfile = Path.Combine(systemDrive, "hiberfil.sys");
        long hiberBytes = 0;
        try
        {
            if (File.Exists(hiberfile))
                hiberBytes = new FileInfo(hiberfile).Length;
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }

        return new DeepCleanSnapshot(
            oldMeasureTask.Result.Bytes,
            hiberBytes,
            doMeasureTask.Result.Bytes,
            hiberBytes > 0);
    }
}
