using System.Runtime.InteropServices;
using SafeCCleaner.Core.Interfaces;
using SafeCCleaner.Core.Models;

namespace SafeCCleaner.Core.Rules;

public sealed class RecycleBinCleanupRule : ICleanupRule
{
    public string Id => "recycle-bin";
    public string DisplayName => "Корзина";
    public string Description => "Удалённые пользователем файлы на диске C:. Не выбирается автоматически.";
    public CleanupRisk Risk => CleanupRisk.Advanced;
    public bool RequiresElevation => false;
    public bool DefaultSelected => false;

    public Task<CleanupScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
        var hr = SHQueryRecycleBin("C:\\", ref info);
        return Task.FromResult(hr == 0
            ? new CleanupScanResult(Id, info.i64Size, info.i64NumItems)
            : new CleanupScanResult(Id, 0, 0, "Windows не вернула размер корзины."));
    }

    public Task<CleanupResult> CleanAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var before = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
        SHQueryRecycleBin("C:\\", ref before);

        const uint flags = 0x00000001 | 0x00000002 | 0x00000004; // no confirmation/progress/sound
        var hr = SHEmptyRecycleBin(IntPtr.Zero, "C:\\", flags);
        if (hr != 0)
            return Task.FromResult(CleanupResult.Failed(Id, $"SHEmptyRecycleBin error: 0x{hr:X8}"));

        return Task.FromResult(new CleanupResult(Id, true, before.i64Size, before.i64NumItems, 0));
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);
}
