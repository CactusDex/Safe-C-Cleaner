using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SafeCCleaner.App.Services;
using SafeCCleaner.App.ViewModels;
using SafeCCleaner.Core.Interfaces;
using SafeCCleaner.Core.Models;
using SafeCCleaner.Core.Services;
using SafeCCleaner.Data;

namespace SafeCCleaner.App;

public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly IReadOnlyList<ICleanupRule> _rules = CleanupRuleCatalog.CreateDefault();
    private readonly DiskScanner _diskScanner = new();
    private readonly DeepCleanInspector _deepInspector = new();
    private readonly SnapshotRepository _repository = new();
    private readonly ElevatedRunner _elevatedRunner = new();
    private readonly StorageComparisonService _storageComparisonService = new();
    private readonly StorageDirectoryScanner _storageDirectoryScanner = new();
    private readonly StorageAnalyzerService _storageAnalyzerService = new();
    private readonly StorageAnalyzerCacheService _storageAnalyzerCacheService = new();
    private readonly AppSettingsService _settingsService = new();
    private readonly AutostartService _autostartService = new();
    private readonly DeveloperStorageService _developerStorageService = new();
    private AppSettings _settings = new();
    private readonly Dictionary<string, (DateTimeOffset CapturedAt, IReadOnlyList<StorageDirectoryEntry> Entries)> _storageDrilldownCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _storageExcludedPaths = [];
    private IReadOnlyList<StorageDirectoryEntry> _currentDrilldownEntries = [];
    private string? _currentDrilldownPath;
    private string _drilldownSearch = string.Empty;
    private long _drilldownMinBytes;
    private string _drilldownSort = "size";
    private double _drilldownProgressPercent;
    private string _drilldownAccessText = string.Empty;
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _drilldownCts;
    private CancellationTokenSource? _developerStorageCts;
    private CancellationTokenSource? _analyzerCts;
    private bool _isDeveloperBusy;
    private bool _dockerAvailable;
    private bool _wslAvailable;
    private bool _npmAvailable;
    private bool _nugetAvailable;
    private bool _androidAvailable;
    private string? _androidSdkPath;
    private string _dockerStorageText = "Нажмите «Обновить» для анализа.";
    private string _wslStorageText = "Нажмите «Обновить» для анализа.";
    private string _npmStorageText = "Нажмите «Обновить» для анализа.";
    private string _nugetStorageText = "Нажмите «Обновить» для анализа.";
    private string _androidStorageText = "Нажмите «Обновить» для анализа.";
    private string _developerStorageStatus = "Developer Storage ещё не анализировался.";
    private IReadOnlyList<StorageAnalyzerEntry> _analyzerFiles = [];
    private IReadOnlyList<StorageAnalyzerEntry> _analyzerFolders = [];
    private bool _isAnalyzerScanning;
    private double _analyzerProgressPercent;
    private string _analyzerStatusText = "Нажмите «Анализировать C:», чтобы найти крупные файлы и папки.";
    private string _analyzerSummaryText = "Анализ ещё не выполнялся";
    private string _analyzerProgressText = string.Empty;
    private string _analyzerLastRunText = "Нет сохранённого анализа";
    private bool _hasAnalyzerCache;
    private DateTimeOffset _analyzerStartedAt;
    private bool _applyingSettings;
    private bool _allowClose;
    private readonly bool _startHidden = Environment.GetCommandLineArgs().Any(x => string.Equals(x, "--background", StringComparison.OrdinalIgnoreCase));
    private IntPtr _trayIconHandle;
    private bool _trayIconAdded;

    private string _driveSummary = "Сканирование ещё не выполнено";
    private double _driveUsedPercent;
    private string _safeReclaimableText = "—";
    private string _statusText = "Готово к сканированию.";
    private string _biggestGrowthText = "Нужны два снимка";
    private string _biggestGrowthDetail = "После следующего сканирования появится сравнение роста каталогов.";
    private string _driveUsedText = "—";
    private string _driveFreeText = "—";
    private string _driveTotalText = "—";
    private string _trend1DayText = "Недостаточно данных";
    private string _trend7DayText = "Недостаточно данных";
    private string _trend30DayText = "Недостаточно данных";
    private string _trend1DayDetail = "Нужен снимок старше суток.";
    private string _trend7DayDetail = "Нужен снимок старше 7 дней.";
    private string _trend30DayDetail = "Нужен снимок старше 30 дней.";
    private string _storageInsightSummary = "После сканирования здесь появится объяснение изменения занятого места.";
    private string _storageBaselineText = "История ещё не накоплена";
    private string _drilldownTitle = "Разбор каталога";
    private string _drilldownStatus = "Нажмите «Разобрать» у интересующей зоны. Ничего удаляться не будет.";
    private bool _isScanning;
    private bool _isDrilldownScanning;
    private double _scanProgressPercent;
    private string _scanEtaText = string.Empty;
    private DateTimeOffset _scanStartedAt;
    private const int MinimumWindowWidth = 1240;
    private const int MinimumWindowHeight = 850;
    private const int GwlpWndProc = -4;
    private const uint WmGetMinMaxInfo = 0x0024;
    private const uint WmClose = 0x0010;
    private const uint WmTrayCallback = 0x8001;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmLButtonDblClk = 0x0203;
    private const uint WmRButtonUp = 0x0205;
    private const uint NimAdd = 0x00000000;
    private const uint NimDelete = 0x00000002;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint MfString = 0x00000000;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCmd = 0x0100;
    private const int SwRestore = 9;
    private const uint TrayOpenCommand = 1001;
    private const uint TrayScanCommand = 1002;
    private const uint TrayExitCommand = 1003;
    private IntPtr _hwnd;
    private IntPtr _previousWndProc;
    private WndProcDelegate? _wndProcDelegate;

    private string _lastScanTimeText = "Ещё не выполнялось";
    private string _lastScanDurationText = "—";
    private string _scanResultSummary = "Нет результатов";
    private string _componentStoreStatusText = "Нажмите «Анализировать», чтобы Windows через DISM проверила необходимость очистки.";
    private string _windowsUpdateCleanupStatusText = "Штатная категория Disk Cleanup: только устаревшие файлы Windows Update.";
    private bool _componentCleanupEnabled;
    private string _windowsOldSizeText = "Проверка...";
    private string _windowsOldStatusText = "Предыдущая установка Windows ещё не проверена.";
    private bool _windowsOldAvailable;
    private string _deliveryOptimizationSizeText = "Проверка...";
    private string _deliveryOptimizationStatusText = "Кэш загрузок Windows Update и Microsoft Store.";
    private string _hiberfileSizeText = "Проверка...";
    private string _hibernationStatusText = "Состояние гибернации ещё не проверено.";
    private bool _hibernationEnabled;
    private string _deepPotentialText = "—";
    private string _componentStoreDetailsText = string.Empty;

    private string _lastCleanupSummary = "Очистка ещё не запускалась";
    private string _lastCleanupDetail = "После очистки здесь появится фактическое изменение свободного места на диске C:.";
    private string _selectedCleanupSummary = "Выбрано: 0 категорий";
    private string _selectedCleanupSizeText = "Найдено: 0 B";
    private bool _canCleanSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CleanupItemViewModel> CleanupItems { get; } = [];
    public ObservableCollection<CleanupItemViewModel> VisibleCleanupItems { get; } = [];
    public ObservableCollection<FolderUsageViewModel> FolderItems { get; } = [];
    public ObservableCollection<StorageGrowthViewModel> StorageGrowthItems { get; } = [];
    public ObservableCollection<StorageDirectoryEntryViewModel> DrilldownItems { get; } = [];
    public ObservableCollection<StorageBreadcrumbViewModel> StorageBreadcrumbs { get; } = [];
    public ObservableCollection<string> StorageExclusionItems { get; } = [];
    public ObservableCollection<string> AndroidSdkPackageItems { get; } = [];
    public ObservableCollection<StorageAnalyzerItemViewModel> AnalyzerFileItems { get; } = [];
    public ObservableCollection<StorageAnalyzerItemViewModel> AnalyzerFolderItems { get; } = [];
    public ObservableCollection<CleanupHistoryViewModel> CleanupHistoryItems { get; } = [];
    private string _cleanupFilter = "all";

    public string SelectedCleanupSummary { get => _selectedCleanupSummary; private set => Set(ref _selectedCleanupSummary, value); }
    public string SelectedCleanupSizeText { get => _selectedCleanupSizeText; private set => Set(ref _selectedCleanupSizeText, value); }
    public bool CanCleanSelected { get => _canCleanSelected; private set => Set(ref _canCleanSelected, value); }

    public string DriveSummary { get => _driveSummary; private set => Set(ref _driveSummary, value); }
    public double DriveUsedPercent { get => _driveUsedPercent; private set => Set(ref _driveUsedPercent, value); }
    public string SafeReclaimableText { get => _safeReclaimableText; private set => Set(ref _safeReclaimableText, value); }
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public string BiggestGrowthText { get => _biggestGrowthText; private set => Set(ref _biggestGrowthText, value); }
    public string BiggestGrowthDetail { get => _biggestGrowthDetail; private set => Set(ref _biggestGrowthDetail, value); }
    public string DriveUsedText { get => _driveUsedText; private set => Set(ref _driveUsedText, value); }
    public string DriveFreeText { get => _driveFreeText; private set => Set(ref _driveFreeText, value); }
    public string DriveTotalText { get => _driveTotalText; private set => Set(ref _driveTotalText, value); }
    public string Trend1DayText { get => _trend1DayText; private set => Set(ref _trend1DayText, value); }
    public string Trend7DayText { get => _trend7DayText; private set => Set(ref _trend7DayText, value); }
    public string Trend30DayText { get => _trend30DayText; private set => Set(ref _trend30DayText, value); }
    public string Trend1DayDetail { get => _trend1DayDetail; private set => Set(ref _trend1DayDetail, value); }
    public string Trend7DayDetail { get => _trend7DayDetail; private set => Set(ref _trend7DayDetail, value); }
    public string Trend30DayDetail { get => _trend30DayDetail; private set => Set(ref _trend30DayDetail, value); }
    public string StorageInsightSummary { get => _storageInsightSummary; private set => Set(ref _storageInsightSummary, value); }
    public string StorageBaselineText { get => _storageBaselineText; private set => Set(ref _storageBaselineText, value); }
    public string DrilldownTitle { get => _drilldownTitle; private set => Set(ref _drilldownTitle, value); }
    public string DrilldownStatus { get => _drilldownStatus; private set => Set(ref _drilldownStatus, value); }
    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (Set(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(ScanProgressVisibility));
                OnPropertyChanged(nameof(CanStartScan));
                OnPropertyChanged(nameof(ScanButtonText));
                OnPropertyChanged(nameof(RescanButtonText));
                OnPropertyChanged(nameof(CanStartAnalyzer));
            }
        }
    }
    public double ScanProgressPercent { get => _scanProgressPercent; private set => Set(ref _scanProgressPercent, value); }
    public string ScanEtaText { get => _scanEtaText; private set => Set(ref _scanEtaText, value); }
    public Visibility ScanProgressVisibility => IsScanning ? Visibility.Visible : Visibility.Collapsed;
    public bool CanStartScan => !IsScanning && !IsAnalyzerScanning;
    public string ScanButtonText => IsScanning ? "Сканирование…" : "Сканировать";
    public string RescanButtonText => IsScanning ? "Сканирование…" : "Пересканировать";
    public string AboutVersionText => "0.5.2 (Analyzer Filter & Icon Polish)";
    public string AboutDeviceText => $"{RuntimeInformation.OSDescription.Trim()} · {RuntimeInformation.OSArchitecture}";
    public string AboutMachineName => Environment.MachineName;
    public string AboutHardwareText => $"{RuntimeInformation.ProcessArchitecture} · {Environment.ProcessorCount} логических процессоров";
    public string AboutDotNetText => $".NET {Environment.Version}";
    public string AboutWindowsAppSdkText => typeof(Application).Assembly.GetName().Version?.ToString() ?? "не определена";
    public string LastScanTimeText { get => _lastScanTimeText; private set => Set(ref _lastScanTimeText, value); }
    public string LastScanDurationText { get => _lastScanDurationText; private set => Set(ref _lastScanDurationText, value); }
    public string ScanResultSummary { get => _scanResultSummary; private set => Set(ref _scanResultSummary, value); }
    public bool IsDrilldownScanning { get => _isDrilldownScanning; private set { if (Set(ref _isDrilldownScanning, value)) OnPropertyChanged(nameof(CanNavigateStorageBack)); } }
    public double DrilldownProgressPercent { get => _drilldownProgressPercent; private set => Set(ref _drilldownProgressPercent, value); }
    public string DrilldownAccessText { get => _drilldownAccessText; private set => Set(ref _drilldownAccessText, value); }
    public bool CanNavigateStorageBack => !IsDrilldownScanning && !string.IsNullOrWhiteSpace(_currentDrilldownPath) && Directory.GetParent(_currentDrilldownPath!) is not null;

    public string ComponentStoreStatusText { get => _componentStoreStatusText; private set => Set(ref _componentStoreStatusText, value); }
    public string WindowsUpdateCleanupStatusText { get => _windowsUpdateCleanupStatusText; private set => Set(ref _windowsUpdateCleanupStatusText, value); }
    public bool ComponentCleanupEnabled { get => _componentCleanupEnabled; private set => Set(ref _componentCleanupEnabled, value); }
    public string WindowsOldSizeText { get => _windowsOldSizeText; private set => Set(ref _windowsOldSizeText, value); }
    public string WindowsOldStatusText { get => _windowsOldStatusText; private set => Set(ref _windowsOldStatusText, value); }
    public bool WindowsOldAvailable { get => _windowsOldAvailable; private set => Set(ref _windowsOldAvailable, value); }
    public string DeliveryOptimizationSizeText { get => _deliveryOptimizationSizeText; private set => Set(ref _deliveryOptimizationSizeText, value); }
    public string DeliveryOptimizationStatusText { get => _deliveryOptimizationStatusText; private set => Set(ref _deliveryOptimizationStatusText, value); }
    public string HiberfileSizeText { get => _hiberfileSizeText; private set => Set(ref _hiberfileSizeText, value); }
    public string HibernationStatusText { get => _hibernationStatusText; private set => Set(ref _hibernationStatusText, value); }
    public bool HibernationEnabled
    {
        get => _hibernationEnabled;
        private set
        {
            if (Set(ref _hibernationEnabled, value))
                OnPropertyChanged(nameof(HibernationRestoreEnabled));
        }
    }
    public bool HibernationRestoreEnabled => !HibernationEnabled;
    public string DeepPotentialText { get => _deepPotentialText; private set => Set(ref _deepPotentialText, value); }
    public string ComponentStoreDetailsText { get => _componentStoreDetailsText; private set => Set(ref _componentStoreDetailsText, value); }
    public string LastCleanupSummary { get => _lastCleanupSummary; private set => Set(ref _lastCleanupSummary, value); }
    public string LastCleanupDetail { get => _lastCleanupDetail; private set => Set(ref _lastCleanupDetail, value); }
    public bool IsDeveloperBusy
    {
        get => _isDeveloperBusy;
        private set
        {
            if (Set(ref _isDeveloperBusy, value))
            {
                OnPropertyChanged(nameof(CanUseDeveloperStorage));
                OnPropertyChanged(nameof(CanCleanDocker));
                OnPropertyChanged(nameof(CanCleanNpm));
                OnPropertyChanged(nameof(CanCleanNuGet));
                OnPropertyChanged(nameof(CanManageAndroid));
            }
        }
    }
    public bool CanUseDeveloperStorage => !IsDeveloperBusy;
    public bool DockerAvailable { get => _dockerAvailable; private set { if (Set(ref _dockerAvailable, value)) OnPropertyChanged(nameof(CanCleanDocker)); } }
    public bool WslAvailable { get => _wslAvailable; private set => Set(ref _wslAvailable, value); }
    public bool NpmAvailable { get => _npmAvailable; private set { if (Set(ref _npmAvailable, value)) OnPropertyChanged(nameof(CanCleanNpm)); } }
    public bool NuGetAvailable { get => _nugetAvailable; private set { if (Set(ref _nugetAvailable, value)) OnPropertyChanged(nameof(CanCleanNuGet)); } }
    public bool AndroidAvailable { get => _androidAvailable; private set { if (Set(ref _androidAvailable, value)) OnPropertyChanged(nameof(CanManageAndroid)); } }
    public bool CanCleanDocker => DockerAvailable && !IsDeveloperBusy;
    public bool CanCleanNpm => NpmAvailable && !IsDeveloperBusy;
    public bool CanCleanNuGet => NuGetAvailable && !IsDeveloperBusy;
    public bool CanManageAndroid => AndroidAvailable && !IsDeveloperBusy;
    public string DockerStorageText { get => _dockerStorageText; private set => Set(ref _dockerStorageText, value); }
    public string WslStorageText { get => _wslStorageText; private set => Set(ref _wslStorageText, value); }
    public string NpmStorageText { get => _npmStorageText; private set => Set(ref _npmStorageText, value); }
    public string NuGetStorageText { get => _nugetStorageText; private set => Set(ref _nugetStorageText, value); }
    public string AndroidStorageText { get => _androidStorageText; private set => Set(ref _androidStorageText, value); }
    public string DeveloperStorageStatus { get => _developerStorageStatus; private set => Set(ref _developerStorageStatus, value); }
    public bool IsAnalyzerScanning
    {
        get => _isAnalyzerScanning;
        private set
        {
            if (Set(ref _isAnalyzerScanning, value))
            {
                OnPropertyChanged(nameof(CanStartAnalyzer));
                OnPropertyChanged(nameof(AnalyzerProgressVisibility));
                OnPropertyChanged(nameof(AnalyzerScanButtonText));
                OnPropertyChanged(nameof(CanStartScan));
            }
        }
    }
    public bool CanStartAnalyzer => !IsAnalyzerScanning && !IsScanning;
    public Visibility AnalyzerProgressVisibility => IsAnalyzerScanning ? Visibility.Visible : Visibility.Collapsed;
    public double AnalyzerProgressPercent { get => _analyzerProgressPercent; private set => Set(ref _analyzerProgressPercent, value); }
    public string AnalyzerStatusText { get => _analyzerStatusText; private set => Set(ref _analyzerStatusText, value); }
    public string AnalyzerSummaryText { get => _analyzerSummaryText; private set => Set(ref _analyzerSummaryText, value); }
    public string AnalyzerProgressText { get => _analyzerProgressText; private set => Set(ref _analyzerProgressText, value); }
    public string AnalyzerLastRunText { get => _analyzerLastRunText; private set => Set(ref _analyzerLastRunText, value); }
    public string AnalyzerScanButtonText => IsAnalyzerScanning ? "Анализ…" : (_hasAnalyzerCache ? "Пересканировать C:" : "Анализировать C:");

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsService.Load();
        _settings.StartWithWindows = _autostartService.IsEnabled();
        try { SystemBackdrop = new MicaBackdrop(); } catch { }
        ConfigureWindowShell();
        InitializeTrayIcon();
        RootGrid.DataContext = this;
        foreach (var rule in _rules)
        {
            var item = new CleanupItemViewModel(rule);
            item.PropertyChanged += CleanupItem_PropertyChanged;
            CleanupItems.Add(item);
        }
        ApplyCleanupFilter();
        LoadStorageExclusions();
        ApplySettingsToUi();
        RootNavigation.Loaded += (_, _) => DispatcherQueue.TryEnqueue(HideNavigationPaneScrollbars);
        RootNavigation.SizeChanged += (_, _) => DispatcherQueue.TryEnqueue(HideNavigationPaneScrollbars);

        Activated += MainWindow_Activated;
        Closed += MainWindow_Closed;
    }

    private void ConfigureWindowShell()
    {
        var width = Math.Max(MinimumWindowWidth, _settings.WindowWidth);
        var height = Math.Max(MinimumWindowHeight, _settings.WindowHeight);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
        RestoreWindowPosition(width, height);
        InstallMinimumSizeHook();
        ApplyWindowIcon();

        var titleBar = AppWindow.TitleBar;
        if (titleBar is null)
            return;

        var background = ColorHelper.FromArgb(255, 31, 31, 31);
        var inactive = ColorHelper.FromArgb(255, 37, 37, 37);
        var hover = ColorHelper.FromArgb(255, 49, 49, 49);
        var foreground = ColorHelper.FromArgb(255, 245, 245, 245);
        var inactiveForeground = ColorHelper.FromArgb(255, 170, 170, 170);

        titleBar.BackgroundColor = background;
        titleBar.InactiveBackgroundColor = inactive;
        titleBar.ForegroundColor = foreground;
        titleBar.InactiveForegroundColor = inactiveForeground;
        titleBar.ButtonBackgroundColor = background;
        titleBar.ButtonInactiveBackgroundColor = inactive;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonInactiveForegroundColor = inactiveForeground;
        titleBar.ButtonHoverBackgroundColor = hover;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedBackgroundColor = hover;
        titleBar.ButtonPressedForegroundColor = foreground;
    }


    private void ApplyWindowIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "SafeCCleaner.ico");
            if (File.Exists(iconPath))
                AppWindow.SetIcon(iconPath);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Could not apply application window icon.", ex);
        }
    }

    private void InstallMinimumSizeHook()
    {
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (_hwnd == IntPtr.Zero)
            return;

        _wndProcDelegate = MinimumSizeWndProc;
        _previousWndProc = SetWindowLongPtr(_hwnd, GwlpWndProc, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
    }

    private IntPtr MinimumSizeWndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == SingleInstanceService.ShowExistingWindowMessage)
        {
            DispatcherQueue.TryEnqueue(ShowFromTray);
            return IntPtr.Zero;
        }

        if (message == WmClose && _settings.MinimizeToTrayOnClose && !_allowClose)
        {
            SaveWindowState();
            AppWindow.Hide();
            StatusText = "Safe C: Cleaner работает в системном трее.";
            return IntPtr.Zero;
        }

        if (message == WmTrayCallback)
        {
            var mouseMessage = unchecked((uint)lParam.ToInt64());
            if (mouseMessage == WmLButtonUp || mouseMessage == WmLButtonDblClk)
            {
                DispatcherQueue.TryEnqueue(ShowFromTray);
                return IntPtr.Zero;
            }
            if (mouseMessage == WmRButtonUp)
            {
                ShowTrayMenu();
                return IntPtr.Zero;
            }
        }

        if (message == WmGetMinMaxInfo && lParam != IntPtr.Zero)
        {
            var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            info.MinTrackSize.X = MinimumWindowWidth;
            info.MinTrackSize.Y = MinimumWindowHeight;
            Marshal.StructureToPtr(info, lParam, false);
        }

        return _previousWndProc != IntPtr.Zero
            ? CallWindowProc(_previousWndProc, hwnd, message, wParam, lParam)
            : DefWindowProc(hwnd, message, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr newLong);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr previousWndProc, IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref NativeRect rect, uint flags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData lpData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string pszExeFileName, uint nIconIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private void InitializeTrayIcon()
    {
        if (_hwnd == IntPtr.Zero || _trayIconAdded) return;
        try
        {
            var exe = Environment.ProcessPath;
            _trayIconHandle = !string.IsNullOrWhiteSpace(exe) ? ExtractIcon(IntPtr.Zero, exe, 0) : IntPtr.Zero;
            var data = CreateTrayIconData();
            if (Shell_NotifyIcon(NimAdd, ref data))
                _trayIconAdded = true;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Could not initialize system tray icon.", ex);
        }
    }

    private NotifyIconData CreateTrayIconData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
        hWnd = _hwnd,
        uID = 1,
        uFlags = NifMessage | NifIcon | NifTip,
        uCallbackMessage = WmTrayCallback,
        hIcon = _trayIconHandle,
        szTip = "Safe C: Cleaner",
        szInfo = string.Empty,
        szInfoTitle = string.Empty
    };

    private void RemoveTrayIcon()
    {
        if (_trayIconAdded)
        {
            var data = CreateTrayIconData();
            Shell_NotifyIcon(NimDelete, ref data);
            _trayIconAdded = false;
        }
        if (_trayIconHandle != IntPtr.Zero)
        {
            DestroyIcon(_trayIconHandle);
            _trayIconHandle = IntPtr.Zero;
        }
    }

    private void ShowFromTray()
    {
        try
        {
            if (_hwnd != IntPtr.Zero)
                ShowWindow(_hwnd, SwRestore);
            AppWindow.Show();
            Activate();
            SetForegroundWindow(_hwnd);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Could not restore app from tray.", ex);
        }
    }

    private void ShowTrayMenu()
    {
        if (!GetCursorPos(out var point)) return;
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) return;
        try
        {
            AppendMenu(menu, MfString, (UIntPtr)TrayOpenCommand, "Открыть Safe C: Cleaner");
            AppendMenu(menu, MfString, (UIntPtr)TrayScanCommand, "Запустить сканирование");
            AppendMenu(menu, MfString, (UIntPtr)TrayExitCommand, "Выйти");
            SetForegroundWindow(_hwnd);
            var command = TrackPopupMenu(menu, TpmRightButton | TpmReturnCmd, point.X, point.Y, 0, _hwnd, IntPtr.Zero);
            switch (command)
            {
                case TrayOpenCommand:
                    DispatcherQueue.TryEnqueue(ShowFromTray);
                    break;
                case TrayScanCommand:
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        ShowFromTray();
                        if (CanStartScan) _ = ScanAllAsync();
                    });
                    break;
                case TrayExitCommand:
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        _allowClose = true;
                        SaveWindowState();
                        RemoveTrayIcon();
                        Close();
                    });
                    break;
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private bool _firstActivation = true;
    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (!_firstActivation)
            return;
        _firstActivation = false;

        try
        {
            if (_startHidden)
                AppWindow.Hide();

            DiagnosticLog.Write("Initializing history database.");
            await _repository.InitializeAsync();
            DiagnosticLog.Write("History database initialized.");
            await RefreshHistoryAsync();
            await LoadLastDiskSnapshotAsync();
            LoadLastAnalyzerResult();

            if (_settings.AutoScanOnStartup)
                await ScanAllAsync();
            else
                StatusText = LastScanTimeText == "Ещё не выполнялось"
                    ? "Автосканирование отключено. Готово."
                    : "Показаны сохранённые данные последнего анализа. Автосканирование отключено.";

        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Startup initialization failed.", ex);
            StatusText = "Ошибка запуска: " + ex.Message + " · Подробности: " + DiagnosticLog.LogPath;
        }
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = (args.SelectedItemContainer as NavigationViewItem)?.Tag?.ToString() ?? "overview";
        OverviewPage.Visibility = tag == "overview" ? Visibility.Visible : Visibility.Collapsed;
        QuickCleanPage.Visibility = tag == "quick" ? Visibility.Visible : Visibility.Collapsed;
        DeepCleanPage.Visibility = tag == "deep" ? Visibility.Visible : Visibility.Collapsed;
        DiskPage.Visibility = tag == "disk" ? Visibility.Visible : Visibility.Collapsed;
        AnalyzerPage.Visibility = tag == "analyzer" ? Visibility.Visible : Visibility.Collapsed;
        DeveloperPage.Visibility = tag == "developer" ? Visibility.Visible : Visibility.Collapsed;
        HistoryPage.Visibility = tag == "history" ? Visibility.Visible : Visibility.Collapsed;
        SafetyPage.Visibility = tag == "safety" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
        AboutPage.Visibility = tag == "about" ? Visibility.Visible : Visibility.Collapsed;
        _settings.LastSection = tag;
        _settingsService.Save(_settings);
        DispatcherQueue.TryEnqueue(HideNavigationPaneScrollbars);
        if (tag == "developer" && DeveloperStorageStatus == "Developer Storage ещё не анализировался." && !IsDeveloperBusy)
            _ = RefreshDeveloperStorageAsync();
    }

    private void HideNavigationPaneScrollbars()
    {
        try
        {
            HideNarrowScrollViewers(RootNavigation);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Could not hide NavigationView pane scrollbars.", ex);
        }
    }

    private static void HideNarrowScrollViewers(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer scroll && scroll.ActualWidth > 0 && scroll.ActualWidth <= 250)
            {
                scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            }
            HideNarrowScrollViewers(child);
        }
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (IsScanning)
            return;
        await ScanAllAsync();
    }

    private void CancelScan_Click(object sender, RoutedEventArgs e)
    {
        _scanCts?.Cancel();
        _drilldownCts?.Cancel();
    }

    private void UpdateScanProgress(double percent, string? status = null)
    {
        ScanProgressPercent = Math.Clamp(percent, 0, 100);
        if (!string.IsNullOrWhiteSpace(status))
            StatusText = status;

        var elapsed = DateTimeOffset.Now - _scanStartedAt;
        if (ScanProgressPercent < 5 || elapsed.TotalSeconds < 3)
        {
            ScanEtaText = "Расчёт…";
            return;
        }

        var totalSeconds = elapsed.TotalSeconds / (ScanProgressPercent / 100d);
        var remainingSeconds = Math.Max(0, totalSeconds - elapsed.TotalSeconds);
        ScanEtaText = FormatEta(TimeSpan.FromSeconds(remainingSeconds));
    }

    private static string FormatEta(TimeSpan eta)
    {
        if (eta.TotalSeconds < 1)
            return "<1 сек";
        if (eta.TotalSeconds < 60)
            return $"~{Math.Ceiling(eta.TotalSeconds):0} сек";
        if (eta.TotalMinutes < 10)
            return $"~{(int)eta.TotalMinutes} мин {eta.Seconds:00} сек";
        return $"~{Math.Ceiling(eta.TotalMinutes):0} мин";
    }

    private async Task ScanAllAsync()
    {
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        try
        {
            IsScanning = true;
            _scanStartedAt = DateTimeOffset.Now;
            ScanProgressPercent = 0;
            ScanEtaText = "Расчёт…";
            StatusText = "Сканирование: безопасные категории";
            var cleanupIndex = 0;
            foreach (var item in CleanupItems)
            {
                UpdateScanProgress(cleanupIndex * 20d / Math.Max(1, CleanupItems.Count), $"Сканирование: {item.Name}");
                var result = await item.Rule.ScanAsync(token);
                item.ReclaimableBytes = result.ReclaimableBytes;
                item.FileCount = result.FileCount;
                item.Warning = result.Warning;
                cleanupIndex++;
                UpdateScanProgress(cleanupIndex * 20d / Math.Max(1, CleanupItems.Count));
            }
            UpdateReclaimableTotals();

            UpdateScanProgress(22, "Сканирование: Deep Clean");
            await RefreshDeepCleanAsync(token);
            UpdateScanProgress(30);

            var progress = new Progress<DiskScanProgress>(p =>
            {
                var diskPart = p.Total == 0 ? 0 : p.Completed * 70d / p.Total;
                UpdateScanProgress(30 + diskPart, $"Сканирование: {p.DisplayName}");
            });
            var snapshot = await _diskScanner.ScanAsync("C:\\", progress, token);
            var previous = await _repository.GetPreviousAsync(snapshot.CapturedAt);
            var oneDay = await _repository.GetNearestAsync(snapshot.CapturedAt.AddDays(-1), TimeSpan.FromHours(12));
            var sevenDays = await _repository.GetNearestAsync(snapshot.CapturedAt.AddDays(-7), TimeSpan.FromDays(2));
            var thirtyDays = await _repository.GetNearestAsync(snapshot.CapturedAt.AddDays(-30), TimeSpan.FromDays(5));
            var comparisonBaseline = sevenDays ?? previous;

            StorageGrowthItems.Clear();
            var storageGrowth = _storageComparisonService.Compare(snapshot, comparisonBaseline);
            foreach (var growth in storageGrowth.Where(x => x.DeltaBytes > 0).Take(8))
                StorageGrowthItems.Add(new StorageGrowthViewModel(growth));

            StorageInsightSummary = _storageComparisonService.BuildSummary(snapshot, comparisonBaseline, storageGrowth);
            StorageBaselineText = comparisonBaseline is null
                ? "Первый снимок — сравнение появится после накопления истории"
                : sevenDays is not null
                    ? $"Baseline около 7 дней назад: {comparisonBaseline.CapturedAt.LocalDateTime:dd.MM.yyyy HH:mm}"
                    : $"7-дневного baseline пока нет · сравнение с предыдущим: {comparisonBaseline.CapturedAt.LocalDateTime:dd.MM.yyyy HH:mm}";

            FolderItems.Clear();
            var previousByKey = previous?.Folders.ToDictionary(x => x.Key, x => x.Bytes)
                ?? new Dictionary<string, long>();
            foreach (var folder in snapshot.Folders)
            {
                long? oldBytes = previousByKey.TryGetValue(folder.Key, out var value) ? value : null;
                FolderItems.Add(new FolderUsageViewModel(folder, oldBytes));
            }

            await _repository.SaveAsync(snapshot);

            var used = snapshot.TotalBytes - snapshot.FreeBytes;
            DriveUsedPercent = snapshot.TotalBytes == 0 ? 0 : used * 100d / snapshot.TotalBytes;
            DriveSummary = $"Занято {ByteSizeFormatter.Format(used)} из {ByteSizeFormatter.Format(snapshot.TotalBytes)} · свободно {ByteSizeFormatter.Format(snapshot.FreeBytes)}";
            DriveUsedText = ByteSizeFormatter.Format(used);
            DriveFreeText = ByteSizeFormatter.Format(snapshot.FreeBytes);
            DriveTotalText = ByteSizeFormatter.Format(snapshot.TotalBytes);
            UpdateTrend(snapshot, oneDay, 1);
            UpdateTrend(snapshot, sevenDays, 7);
            UpdateTrend(snapshot, thirtyDays, 30);

            var biggestGrowth = FolderItems
                .Where(x => x.GrowthBytes.HasValue)
                .OrderByDescending(x => x.GrowthBytes)
                .FirstOrDefault();

            if (previous is null)
            {
                BiggestGrowthText = "Первый снимок сохранён";
                BiggestGrowthDetail = "При следующем сканировании покажу, какой каталог вырос сильнее всего.";
                StatusText = "Первый снимок сохранён. Готово.";
            }
            else if (biggestGrowth is { GrowthBytes: > 0 })
            {
                BiggestGrowthText = $"{biggestGrowth.Name}: {biggestGrowth.GrowthText}";
                BiggestGrowthDetail = $"С прошлого снимка больше всего вырос каталог {biggestGrowth.Path}.";
                StatusText = $"Готово. Больше всего выросло: {biggestGrowth.Name} ({biggestGrowth.GrowthText}).";
            }
            else
            {
                BiggestGrowthText = "Существенного роста нет";
                BiggestGrowthDetail = "Отслеживаемые каталоги не стали заметно больше относительно прошлого снимка.";
                StatusText = "Готово. Существенного роста не обнаружено.";
            }

            var elapsed = DateTimeOffset.Now - _scanStartedAt;
            LastScanTimeText = DateTimeOffset.Now.LocalDateTime.ToString("dd.MM.yyyy HH:mm");
            LastScanDurationText = FormatDuration(elapsed);
            ScanResultSummary = $"{FolderItems.Count:N0} зон · {SafeReclaimableText} безопасно очищаемо";
            ScanProgressPercent = 100;
            ScanEtaText = "Готово";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Сканирование отменено.";
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Scan failed.", ex);
            StatusText = "Ошибка сканирования: " + ex.Message;
        }
        finally
        {
            IsScanning = false;
        }
    }

    private async Task LoadLastDiskSnapshotAsync()
    {
        try
        {
            var snapshot = await _repository.GetLatestAsync();
            if (snapshot is null)
                return;

            var previous = await _repository.GetPreviousAsync(snapshot.CapturedAt);
            var oneDay = await _repository.GetNearestAsync(snapshot.CapturedAt.AddDays(-1), TimeSpan.FromHours(12));
            var sevenDays = await _repository.GetNearestAsync(snapshot.CapturedAt.AddDays(-7), TimeSpan.FromDays(2));
            var thirtyDays = await _repository.GetNearestAsync(snapshot.CapturedAt.AddDays(-30), TimeSpan.FromDays(5));
            var comparisonBaseline = sevenDays ?? previous;

            StorageGrowthItems.Clear();
            var storageGrowth = _storageComparisonService.Compare(snapshot, comparisonBaseline);
            foreach (var growth in storageGrowth.Where(x => x.DeltaBytes > 0).Take(8))
                StorageGrowthItems.Add(new StorageGrowthViewModel(growth));

            StorageInsightSummary = _storageComparisonService.BuildSummary(snapshot, comparisonBaseline, storageGrowth);
            StorageBaselineText = comparisonBaseline is null
                ? "Сохранён первый снимок — сравнение появится после следующего анализа"
                : sevenDays is not null
                    ? $"Baseline около 7 дней назад: {comparisonBaseline.CapturedAt.LocalDateTime:dd.MM.yyyy HH:mm}"
                    : $"7-дневного baseline пока нет · сравнение с предыдущим: {comparisonBaseline.CapturedAt.LocalDateTime:dd.MM.yyyy HH:mm}";

            FolderItems.Clear();
            var previousByKey = previous?.Folders.ToDictionary(x => x.Key, x => x.Bytes)
                ?? new Dictionary<string, long>();
            foreach (var folder in snapshot.Folders)
            {
                long? oldBytes = previousByKey.TryGetValue(folder.Key, out var value) ? value : null;
                FolderItems.Add(new FolderUsageViewModel(folder, oldBytes));
            }

            var used = snapshot.TotalBytes - snapshot.FreeBytes;
            DriveUsedPercent = snapshot.TotalBytes == 0 ? 0 : used * 100d / snapshot.TotalBytes;
            DriveSummary = $"Занято {ByteSizeFormatter.Format(used)} из {ByteSizeFormatter.Format(snapshot.TotalBytes)} · свободно {ByteSizeFormatter.Format(snapshot.FreeBytes)}";
            DriveUsedText = ByteSizeFormatter.Format(used);
            DriveFreeText = ByteSizeFormatter.Format(snapshot.FreeBytes);
            DriveTotalText = ByteSizeFormatter.Format(snapshot.TotalBytes);
            UpdateTrend(snapshot, oneDay, 1);
            UpdateTrend(snapshot, sevenDays, 7);
            UpdateTrend(snapshot, thirtyDays, 30);

            var biggestGrowth = FolderItems
                .Where(x => x.GrowthBytes.HasValue)
                .OrderByDescending(x => x.GrowthBytes)
                .FirstOrDefault();

            if (previous is null)
            {
                BiggestGrowthText = "Первый снимок сохранён";
                BiggestGrowthDetail = "Для сравнения роста нужен ещё один анализ.";
            }
            else if (biggestGrowth is { GrowthBytes: > 0 })
            {
                BiggestGrowthText = $"{biggestGrowth.Name}: {biggestGrowth.GrowthText}";
                BiggestGrowthDetail = $"По сохранённым снимкам больше всего вырос каталог {biggestGrowth.Path}.";
            }
            else
            {
                BiggestGrowthText = "Существенного роста нет";
                BiggestGrowthDetail = "В сохранённых снимках отслеживаемые каталоги не показывают заметного роста.";
            }

            LastScanTimeText = snapshot.CapturedAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm");
            LastScanDurationText = "сохранено";
            ScanResultSummary = $"{FolderItems.Count:N0} зон · сохранённый снимок";
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Could not restore last disk snapshot.", ex);
        }
    }

    private void LoadLastAnalyzerResult()
    {
        try
        {
            var cache = _storageAnalyzerCacheService.Load();
            if (cache is null)
                return;

            var result = cache.ToResult();
            _analyzerFiles = NormalizeAnalyzerFileCategories(result.LargestFiles);
            _analyzerFolders = result.LargestDirectories;
            _hasAnalyzerCache = true;
            OnPropertyChanged(nameof(AnalyzerScanButtonText));
            ApplyAnalyzerFilters();

            AnalyzerSummaryText = $"Просканировано {ByteSizeFormatter.Format(result.ScannedBytes)} · {result.FileCount:N0} файлов · {result.DirectoryCount:N0} папок";
            var age = DateTimeOffset.Now - cache.CapturedAt;
            var stale = age > TimeSpan.FromHours(24);
            AnalyzerLastRunText = $"Последний анализ: {cache.CapturedAt.LocalDateTime:dd.MM.yyyy HH:mm}" +
                                  (stale ? " · данные могут быть устаревшими" : " · сохранённые данные");
            AnalyzerProgressText = $"Сохранено · {FormatDuration(result.Duration)}";
            AnalyzerStatusText = stale
                ? "Показаны сохранённые результаты. Для актуальных размеров запустите повторный анализ."
                : result.AccessLimitedCount > 0
                    ? $"Показаны сохранённые результаты · недоступных участков: {result.AccessLimitedCount:N0}."
                    : "Показаны сохранённые результаты последнего анализа. Ничего на диске не изменяется.";
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Could not restore Storage Analyzer result.", ex);
        }
    }

    private async Task RefreshDeepCleanAsync(CancellationToken cancellationToken = default)
    {
        var deep = await _deepInspector.ScanAsync(cancellationToken);
        WindowsOldSizeText = ByteSizeFormatter.Format(deep.WindowsOldBytes);
        WindowsOldAvailable = deep.WindowsOldBytes > 0;
        WindowsOldStatusText = WindowsOldAvailable
            ? "Обнаружена предыдущая установка Windows. После её удаления штатный откат к этой версии будет недоступен."
            : "Windows.old не обнаружена.";

        DeliveryOptimizationSizeText = ByteSizeFormatter.Format(deep.DeliveryOptimizationBytes);
        DeliveryOptimizationStatusText = deep.DeliveryOptimizationBytes > 0
            ? "Можно удалить штатным DeliveryOptimization PowerShell-модулем. Закреплённый контент не затрагивается."
            : "Заметного кэша Delivery Optimization не обнаружено.";

        HiberfileSizeText = ByteSizeFormatter.Format(deep.HiberfileBytes);
        HibernationEnabled = deep.HibernationEnabled;
        HibernationStatusText = HibernationEnabled
            ? "Гибернация включена. Отключение освободит hiberfil.sys, но уберёт гибернацию и может повлиять на Fast Startup."
            : "hiberfil.sys отсутствует: гибернация выключена. При необходимости её можно включить обратно.";

        DeepPotentialText = ByteSizeFormatter.Format(deep.WindowsOldBytes + deep.DeliveryOptimizationBytes + deep.HiberfileBytes);
    }

    private async void QuickCleanSafe_Click(object sender, RoutedEventArgs e)
    {
        var safe = CleanupItems
            .Where(x => x.Rule.Risk == CleanupRisk.Safe && x.ReclaimableBytes > 0)
            .ToArray();

        if (safe.Length == 0)
        {
            StatusText = "Безопасных данных для очистки сейчас не найдено.";
            return;
        }

        await CleanItemsAsync(safe);
    }

    private async void CleanSingle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: CleanupItemViewModel item })
            return;

        if (!await ConfirmIfNeededAsync(item))
            return;

        await CleanItemsAsync([item]);
    }

    private async void CleanSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = VisibleCleanupItems.Where(x => x.IsSelected && x.ReclaimableBytes > 0).ToArray();
        if (selected.Length == 0)
        {
            StatusText = "Нечего очищать: выберите категории с найденными данными.";
            return;
        }

        var confirmed = new List<CleanupItemViewModel>();
        foreach (var item in selected)
        {
            if (await ConfirmIfNeededAsync(item))
                confirmed.Add(item);
        }

        await CleanItemsAsync(confirmed);
    }

    private async Task CleanItemsAsync(IEnumerable<CleanupItemViewModel> items)
    {
        var selectedItems = items.ToArray();
        if (selectedItems.Length == 0)
            return;

        var startedAt = DateTimeOffset.Now;
        var beforeFree = GetSystemDriveFreeBytes();
        long totalFreed = 0;
        long totalSkipped = 0;
        var successfulNames = new List<string>();
        var historyItems = new List<CleanupHistoryItem>();

        foreach (var item in selectedItems)
        {
            StatusText = $"Очистка: {item.Name}...";
            CleanupResult result;
            try
            {
                result = item.Rule.RequiresElevation
                    ? await _elevatedRunner.RunCleanupAsync(item.Rule.Id)
                    : await item.Rule.CleanAsync();
            }
            catch (Exception ex)
            {
                result = CleanupResult.Failed(item.Rule.Id, ex.Message);
            }

            historyItems.Add(new CleanupHistoryItem(
                item.Rule.Id,
                item.Name,
                item.Rule.Risk,
                result.Success,
                result.FreedBytes,
                result.DeletedFiles,
                result.SkippedFiles,
                result.Message));

            if (!result.Success)
            {
                StatusText = $"{item.Name}: {result.Message}";
                continue;
            }

            successfulNames.Add(item.Name);
            totalFreed += result.FreedBytes;
            totalSkipped += result.SkippedFiles;
        }

        var afterFree = GetSystemDriveFreeBytes();
        var operationName = selectedItems.Length == 1
            ? selectedItems[0].Name
            : $"Быстрая очистка · {selectedItems.Length} категорий";
        var mode = selectedItems.All(x => x.Rule.Risk == CleanupRisk.Safe) ? "SAFE" : "ADVANCED";
        var success = historyItems.Any(x => x.Success);

        if (!success)
        {
            LastCleanupSummary = "Очистка не выполнена";
            LastCleanupDetail = "Ни одна выбранная категория не завершилась успешно.";
        }
        else
        {
            UpdateCleanupReport(operationName, beforeFree, afterFree, totalFreed, totalSkipped);
        }

        var record = new CleanupHistoryRecord(
            0,
            startedAt,
            DateTimeOffset.Now,
            operationName,
            mode,
            success,
            beforeFree,
            afterFree,
            totalFreed,
            totalSkipped,
            LastCleanupDetail,
            historyItems);

        await SaveCleanupHistoryAsync(record);
        await ShowCleanupResultDialogAsync(record);

        StatusText = $"Очистка завершена: удалено по правилам {ByteSizeFormatter.Format(totalFreed)}, пропущено объектов: {totalSkipped}. Повторное сканирование...";
        await ScanAllAsync();
    }

    private async void AnalyzeComponentStore_Click(object sender, RoutedEventArgs e)
    {
        StatusText = "DISM анализирует хранилище компонентов...";
        var result = await _elevatedRunner.RunSystemOperationAsync("analyze-component-store");
        ComponentStoreStatusText = result.Message;
        ComponentStoreDetailsText = result.Details ?? string.Empty;
        ComponentCleanupEnabled = result.Success && result.Message.StartsWith("Windows рекомендует", StringComparison.OrdinalIgnoreCase);
        StatusText = result.Success ? "Анализ WinSxS завершён." : "Ошибка анализа WinSxS: " + result.Message;
    }

    private async void CleanupComponentStore_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmSystemActionAsync(
                "Очистить старые компоненты Windows?",
                "Будет выполнен DISM /StartComponentCleanup. Заменённые версии компонентов удалятся сразу. /ResetBase не используется.",
                "Очистить"))
            return;

        var startedAt = DateTimeOffset.Now;
        var beforeFree = GetSystemDriveFreeBytes();
        StatusText = "DISM выполняет StartComponentCleanup. Не закрывайте приложение...";
        var result = await _elevatedRunner.RunSystemOperationAsync("component-cleanup");
        var afterFree = GetSystemDriveFreeBytes();
        UpdateSystemOperationReport("Очистка старых компонентов Windows", beforeFree, afterFree, result);
        await RecordSystemOperationAsync("Очистка старых компонентов Windows", "DEEP", CleanupRisk.Advanced, startedAt, beforeFree, afterFree, result);
        ComponentStoreStatusText = result.Success
            ? $"{result.Message} Освобождено по свободному месту диска: {ByteSizeFormatter.Format(result.FreedBytes)}."
            : result.Message;
        ComponentCleanupEnabled = false;
        StatusText = result.Success ? "Очистка компонентов завершена." : "Ошибка DISM: " + result.Message;
        await ScanAllAsync();
    }

    private async void CleanWindowsUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmSystemActionAsync(
                "Очистить устаревшие файлы Windows Update?",
                "Windows запустит встроенную категорию Update Cleanup через Disk Cleanup. Приложение не удаляет SoftwareDistribution или WinSxS вручную. Старые заменённые файлы обновлений могут стать недоступны для отката.",
                "Очистить"))
            return;

        var startedAt = DateTimeOffset.Now;
        var beforeFree = GetSystemDriveFreeBytes();
        StatusText = "Windows Update Cleanup выполняется штатным Disk Cleanup...";
        WindowsUpdateCleanupStatusText = "Выполняется очистка Windows Update...";

        var result = await _elevatedRunner.RunSystemOperationAsync("windows-update-cleanup");
        var afterFree = GetSystemDriveFreeBytes();

        UpdateSystemOperationReport("Windows Update Cleanup", beforeFree, afterFree, result);
        await RecordSystemOperationAsync("Windows Update Cleanup", "DEEP", CleanupRisk.Safe, startedAt, beforeFree, afterFree, result);

        WindowsUpdateCleanupStatusText = result.Success
            ? $"{result.Message} Освобождено: {ByteSizeFormatter.Format(result.FreedBytes)}."
            : "Ошибка: " + result.Message;
        StatusText = result.Success
            ? $"Windows Update Cleanup завершена: {ByteSizeFormatter.Format(result.FreedBytes)}."
            : "Ошибка Windows Update Cleanup: " + result.Message;

        if (result.Success)
            await ScanAllAsync();
    }

    private async void CleanWindowsOld_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmSystemActionAsync(
                "Удалить предыдущую установку Windows?",
                "После удаления Windows.old и остальных post-upgrade файлов штатный возврат к предыдущей установке Windows может стать недоступен.",
                "Удалить"))
            return;

        var startedAt = DateTimeOffset.Now;
        var beforeFree = GetSystemDriveFreeBytes();
        StatusText = "Windows очищает остатки предыдущего обновления...";
        var result = await _elevatedRunner.RunSystemOperationAsync("windows-upgrade-leftovers");
        var afterFree = GetSystemDriveFreeBytes();
        UpdateSystemOperationReport("Остатки обновления Windows", beforeFree, afterFree, result);
        await RecordSystemOperationAsync("Остатки обновления Windows", "DEEP", CleanupRisk.Advanced, startedAt, beforeFree, afterFree, result);
        StatusText = result.Success
            ? $"Deep Clean завершён: освобождено {ByteSizeFormatter.Format(result.FreedBytes)}."
            : "Ошибка очистки: " + result.Message;
        await ScanAllAsync();
    }

    private async void CleanDeliveryOptimization_Click(object sender, RoutedEventArgs e)
    {
        var startedAt = DateTimeOffset.Now;
        var beforeFree = GetSystemDriveFreeBytes();
        StatusText = "Очистка Delivery Optimization...";
        var result = await _elevatedRunner.RunSystemOperationAsync("delivery-optimization");
        var afterFree = GetSystemDriveFreeBytes();
        UpdateSystemOperationReport("Delivery Optimization", beforeFree, afterFree, result);
        await RecordSystemOperationAsync("Delivery Optimization", "DEEP", CleanupRisk.Safe, startedAt, beforeFree, afterFree, result);
        DeliveryOptimizationStatusText = result.Success ? result.Message : "Ошибка: " + result.Message;
        StatusText = result.Success
            ? $"Delivery Optimization: освобождено {ByteSizeFormatter.Format(result.FreedBytes)}."
            : result.Message;
        await RefreshDeepCleanAsync();
    }

    private async void DisableHibernate_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmSystemActionAsync(
                "Отключить гибернацию?",
                "Windows удалит hiberfil.sys. Гибернация станет недоступна; Fast Startup также может изменить поведение. Операцию можно отменить кнопкой «Включить обратно».",
                "Отключить"))
            return;

        var startedAt = DateTimeOffset.Now;
        var beforeFree = GetSystemDriveFreeBytes();
        StatusText = "Отключение гибернации...";
        var result = await _elevatedRunner.RunSystemOperationAsync("hibernate-off");
        var afterFree = GetSystemDriveFreeBytes();
        UpdateSystemOperationReport("Отключение гибернации", beforeFree, afterFree, result);
        await RecordSystemOperationAsync("Отключение гибернации", "ADVANCED", CleanupRisk.Advanced, startedAt, beforeFree, afterFree, result);
        StatusText = result.Success
            ? $"Гибернация отключена. Освобождено {ByteSizeFormatter.Format(result.FreedBytes)}."
            : "Ошибка powercfg: " + result.Message;
        await RefreshDeepCleanAsync();
    }

    private async void EnableHibernate_Click(object sender, RoutedEventArgs e)
    {
        var startedAt = DateTimeOffset.Now;
        var beforeFree = GetSystemDriveFreeBytes();
        StatusText = "Включение гибернации...";
        var result = await _elevatedRunner.RunSystemOperationAsync("hibernate-on");
        var afterFree = GetSystemDriveFreeBytes();
        UpdateSystemOperationReport("Включение гибернации", beforeFree, afterFree, result);
        await RecordSystemOperationAsync("Включение гибернации", "SYSTEM", CleanupRisk.Advanced, startedAt, beforeFree, afterFree, result);
        StatusText = result.Success ? result.Message : "Ошибка powercfg: " + result.Message;
        await RefreshDeepCleanAsync();
    }

    private void CleanupItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CleanupItemViewModel.IsSelected) or nameof(CleanupItemViewModel.ReclaimableBytes))
            UpdateSelectedCleanupSummary();
    }

    private void UpdateSelectedCleanupSummary()
    {
        var selected = VisibleCleanupItems
            .Where(x => x.IsSelected)
            .ToArray();

        SelectedCleanupSummary = selected.Length switch
        {
            1 => "Выбрана: 1 категория",
            2 or 3 or 4 => $"Выбрано: {selected.Length} категории",
            _ => $"Выбрано: {selected.Length} категорий"
        };
        SelectedCleanupSizeText = $"Найдено: {ByteSizeFormatter.Format(selected.Sum(x => x.ReclaimableBytes))}";
        CanCleanSelected = selected.Any(x => x.ReclaimableBytes > 0);
    }

    private void CleanupFilter_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: not null } radio)
            _cleanupFilter = radio.Tag.ToString() ?? "all";
        ApplyCleanupFilter();
    }

    private void ApplyCleanupFilter()
    {
        VisibleCleanupItems.Clear();
        foreach (var item in CleanupItems)
        {
            var include = _cleanupFilter switch
            {
                "safe" => item.Rule.Risk == CleanupRisk.Safe,
                "advanced" => item.Rule.Risk == CleanupRisk.Advanced,
                _ => true
            };

            if (include)
                VisibleCleanupItems.Add(item);
        }

        UpdateSelectedCleanupSummary();
    }

    private async void RefreshHistory_Click(object sender, RoutedEventArgs e) => await RefreshHistoryAsync();

    private void UpdateTrend(DiskSnapshot current, DiskSnapshot? baseline, int days)
    {
        string value;
        string detail;

        if (baseline is null)
        {
            value = "Недостаточно данных";
            detail = $"Нужен снимок не новее чем {days} дн. назад.";
        }
        else
        {
            var currentUsed = current.TotalBytes - current.FreeBytes;
            var baselineUsed = baseline.TotalBytes - baseline.FreeBytes;
            var delta = currentUsed - baselineUsed;
            value = FormatSignedBytes(delta);

            var top = FindLargestFolderGrowth(current, baseline);
            detail = top is null
                ? $"С {baseline.CapturedAt.LocalDateTime:dd.MM HH:mm}; заметного роста отслеживаемых каталогов нет."
                : $"С {baseline.CapturedAt.LocalDateTime:dd.MM HH:mm}; больше всего: {top.Value.Name} {FormatSignedBytes(top.Value.Delta)}.";
        }

        switch (days)
        {
            case 1: Trend1DayText = value; Trend1DayDetail = detail; break;
            case 7: Trend7DayText = value; Trend7DayDetail = detail; break;
            case 30: Trend30DayText = value; Trend30DayDetail = detail; break;
        }
    }

    private static (string Name, long Delta)? FindLargestFolderGrowth(DiskSnapshot current, DiskSnapshot baseline)
    {
        var old = baseline.Folders.ToDictionary(x => x.Key, x => x.Bytes, StringComparer.OrdinalIgnoreCase);
        return current.Folders
            .Where(x => old.ContainsKey(x.Key))
            .Select(x => (Name: x.DisplayName, Delta: x.Bytes - old[x.Key]))
            .Where(x => x.Delta > 0)
            .OrderByDescending(x => x.Delta)
            .Select(x => ((string Name, long Delta)?)x)
            .FirstOrDefault();
    }

    private static string FormatSignedBytes(long value) => value switch
    {
        > 0 => "+" + ByteSizeFormatter.Format(value),
        < 0 => "−" + ByteSizeFormatter.Format(Math.Abs(value)),
        _ => "без изменений"
    };


    private async void AnalyzeFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string path } || string.IsNullOrWhiteSpace(path))
            return;

        await AnalyzeFolderAsync(path);
    }

    private async Task AnalyzeFolderAsync(string path, bool forceRefresh = false)
    {
        _drilldownCts?.Cancel();
        _drilldownCts = new CancellationTokenSource();
        var token = _drilldownCts.Token;

        try
        {
            IsDrilldownScanning = true;
            DrilldownProgressPercent = 0;
            DrilldownAccessText = string.Empty;
            _currentDrilldownPath = path;
            OnPropertyChanged(nameof(CanNavigateStorageBack));
            BuildStorageBreadcrumbs(path);
            DrilldownTitle = "Разбор: " + path;
            DrilldownStatus = "Сканирую верхний уровень…";

            IReadOnlyList<StorageDirectoryEntry> entries;
            if (!forceRefresh && _storageDrilldownCache.TryGetValue(path, out var cached) &&
                DateTimeOffset.UtcNow - cached.CapturedAt < TimeSpan.FromMinutes(Math.Clamp(_settings.ScannerCacheMinutes, 1, 240)))
            {
                entries = cached.Entries;
                DrilldownProgressPercent = 100;
                DrilldownStatus = $"Кэш анализа · младше {_settings.ScannerCacheMinutes} мин";
            }
            else
            {
                var progress = new Progress<StorageScanProgress>(p =>
                {
                    DrilldownProgressPercent = p.Percent;
                    DrilldownStatus = $"{p.CompletedItems:N0}/{p.TotalItems:N0} · {p.CurrentName}";
                    DrilldownAccessText = p.AccessLimitedItems > 0 ? $"Ограниченный доступ: {p.AccessLimitedItems:N0}" : string.Empty;
                });
                entries = await _storageDirectoryScanner.ScanChildrenAsync(path, progress, _storageExcludedPaths, token);
                _storageDrilldownCache[path] = (DateTimeOffset.UtcNow, entries);
                DrilldownProgressPercent = 100;
            }

            _currentDrilldownEntries = entries;
            ApplyDrilldownView();
            var limited = entries.Count(x => x.AccessLimited);
            DrilldownAccessText = limited > 0 ? $"Частично недоступно: {limited:N0}" : "Доступ обработан без ошибок";
            DrilldownStatus = entries.Count == 0
                ? "Нет доступных элементов или путь исключён."
                : $"{entries.Count:N0} элементов · {entries.Sum(x => x.FileCount):N0} файлов · {entries.Sum(x => x.DirectoryCount):N0} папок";
        }
        catch (OperationCanceledException)
        {
            DrilldownStatus = "Разбор каталога отменён.";
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Storage drill-down failed.", ex);
            DrilldownStatus = "Ошибка анализа: " + ex.Message;
        }
        finally
        {
            IsDrilldownScanning = false;
        }
    }

    private void ApplyDrilldownView()
    {
        IEnumerable<StorageDirectoryEntry> query = _currentDrilldownEntries;
        if (!string.IsNullOrWhiteSpace(_drilldownSearch))
            query = query.Where(x => x.Name.Contains(_drilldownSearch, StringComparison.CurrentCultureIgnoreCase) || x.Path.Contains(_drilldownSearch, StringComparison.CurrentCultureIgnoreCase));
        if (_drilldownMinBytes > 0)
            query = query.Where(x => x.Bytes >= _drilldownMinBytes);

        query = _drilldownSort switch
        {
            "name" => query.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase),
            "files" => query.OrderByDescending(x => x.FileCount).ThenByDescending(x => x.Bytes),
            "folders" => query.OrderByDescending(x => x.IsDirectory).ThenByDescending(x => x.Bytes),
            _ => query.OrderByDescending(x => x.Bytes).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
        };

        DrilldownItems.Clear();
        foreach (var entry in query.Take(250))
            DrilldownItems.Add(new StorageDirectoryEntryViewModel(entry));
    }

    private void BuildStorageBreadcrumbs(string path)
    {
        StorageBreadcrumbs.Clear();
        try
        {
            var full = Path.GetFullPath(path).TrimEnd('\\');
            var root = Path.GetPathRoot(full) ?? "C:\\";
            StorageBreadcrumbs.Add(new StorageBreadcrumbViewModel(root, root));
            var relative = full[root.Length..];
            var current = root.TrimEnd('\\');
            foreach (var part in relative.Split('\\', StringSplitOptions.RemoveEmptyEntries))
            {
                current = current + "\\" + part;
                StorageBreadcrumbs.Add(new StorageBreadcrumbViewModel(part, current));
            }
        }
        catch { }
    }

    private async void StorageBack_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentDrilldownPath)) return;
        var parent = Directory.GetParent(_currentDrilldownPath);
        if (parent is not null) await AnalyzeFolderAsync(parent.FullName);
    }

    private async void StorageBreadcrumb_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path }) await AnalyzeFolderAsync(path);
    }

    private async void StorageRefreshFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_currentDrilldownPath))
        {
            _storageDrilldownCache.Remove(_currentDrilldownPath);
            await AnalyzeFolderAsync(_currentDrilldownPath, true);
        }
    }

    private void StorageSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _drilldownSearch = (sender as TextBox)?.Text?.Trim() ?? string.Empty;
        ApplyDrilldownView();
    }

    private void StorageFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox box || box.SelectedItem is not ComboBoxItem item) return;
        _drilldownMinBytes = item.Tag?.ToString() switch
        {
            "100m" => 100L * 1024 * 1024,
            "1g" => 1024L * 1024 * 1024,
            "5g" => 5L * 1024 * 1024 * 1024,
            _ => 0
        };
        ApplyDrilldownView();
    }

    private void StorageSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox box || box.SelectedItem is not ComboBoxItem item) return;
        _drilldownSort = item.Tag?.ToString() ?? "size";
        ApplyDrilldownView();
    }

    private async void StorageExclusions_Click(object sender, RoutedEventArgs e)
    {
        var editor = new TextBox
        {
            Text = string.Join(Environment.NewLine, _storageExcludedPaths),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            MinWidth = 520,
            MinHeight = 220,
            PlaceholderText = "Один полный путь на строку"
        };
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "Исключения Storage Scanner",
            Content = editor,
            PrimaryButtonText = "Сохранить",
            CloseButtonText = "Отмена"
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        _storageExcludedPaths.Clear();
        foreach (var line in editor.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var value = line.Trim();
            if (!string.IsNullOrWhiteSpace(value)) _storageExcludedPaths.Add(value);
        }
        StorageExclusionItems.Clear();
        foreach (var value in _storageExcludedPaths) StorageExclusionItems.Add(value);
        SaveStorageExclusions();
        _storageDrilldownCache.Clear();
        DrilldownStatus = $"Исключений сохранено: {_storageExcludedPaths.Count:N0}";
    }

    private static string FormatDuration(TimeSpan value)
    {
        if (value.TotalSeconds < 60) return $"{Math.Max(1, (int)Math.Round(value.TotalSeconds))} сек";
        return $"{(int)value.TotalMinutes} мин {value.Seconds:00} сек";
    }

    private void ApplySettingsToUi()
    {
        _applyingSettings = true;
        RootNavigation.IsPaneOpen = _settings.IsNavigationPaneOpen;
        AutoScanToggle.IsOn = _settings.AutoScanOnStartup;
        AutostartToggle.IsOn = _settings.StartWithWindows;
        MinimizeToTrayToggle.IsOn = _settings.MinimizeToTrayOnClose;
        foreach (var item in ScannerCacheCombo.Items.OfType<ComboBoxItem>())
        {
            if (int.TryParse(item.Tag?.ToString(), out var minutes) && minutes == _settings.ScannerCacheMinutes)
            {
                ScannerCacheCombo.SelectedItem = item;
                break;
            }
        }
        if (ScannerCacheCombo.SelectedItem is null)
            ScannerCacheCombo.SelectedIndex = 1;

        SelectNavigationSection(_settings.LastSection, save: false);
        _applyingSettings = false;
    }

    private void SelectNavigationSection(string? tag, bool save = true)
    {
        tag = string.IsNullOrWhiteSpace(tag) ? "overview" : tag;
        var items = RootNavigation.MenuItems.Concat(RootNavigation.FooterMenuItems)
            .OfType<NavigationViewItem>();
        var target = items.FirstOrDefault(x => string.Equals(x.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase));
        if (target is not null)
            RootNavigation.SelectedItem = target;
        if (save)
        {
            _settings.LastSection = tag;
            _settingsService.Save(_settings);
        }
    }

    private void AutoScanToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_applyingSettings) return;
        _settings.AutoScanOnStartup = AutoScanToggle.IsOn;
        _settingsService.Save(_settings);
        SettingsStatusText.Text = _settings.AutoScanOnStartup ? "Автосканирование включено." : "Автосканирование отключено.";
    }

    private void AutostartToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_applyingSettings) return;
        try
        {
            _autostartService.SetEnabled(AutostartToggle.IsOn);
            _settings.StartWithWindows = AutostartToggle.IsOn;
            _settingsService.Save(_settings);
            SettingsStatusText.Text = _settings.StartWithWindows
                ? "Автозапуск включён: Safe C: Cleaner будет стартовать в системном трее."
                : "Автозапуск отключён.";
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Could not change autostart.", ex);
            _applyingSettings = true;
            AutostartToggle.IsOn = _autostartService.IsEnabled();
            _applyingSettings = false;
            SettingsStatusText.Text = "Не удалось изменить автозапуск: " + ex.Message;
        }
    }

    private void MinimizeToTrayToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_applyingSettings) return;
        _settings.MinimizeToTrayOnClose = MinimizeToTrayToggle.IsOn;
        _settingsService.Save(_settings);
        SettingsStatusText.Text = _settings.MinimizeToTrayOnClose
            ? "Крестик будет скрывать приложение в системный трей."
            : "Крестик будет полностью закрывать приложение.";
    }

    private void ScannerCacheCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ScannerCacheCombo.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var minutes))
        {
            _settings.ScannerCacheMinutes = Math.Clamp(minutes, 1, 240);
            _storageDrilldownCache.Clear();
            _settingsService.Save(_settings);
            if (SettingsStatusText is not null)
                SettingsStatusText.Text = $"Кэш Scanner: {_settings.ScannerCacheMinutes} мин.";
        }
    }

    private async void ResetSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "Сбросить настройки?",
            Content = "Будут восстановлены настройки интерфейса, автосканирования и кэша. Исключения Storage Scanner останутся без изменений.",
            PrimaryButtonText = "Сбросить",
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        _settingsService.Reset();
        try { _autostartService.SetEnabled(false); } catch (Exception ex) { DiagnosticLog.Write("Could not reset autostart.", ex); }
        _settings = new AppSettings();
        ApplySettingsToUi();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(MinimumWindowWidth, MinimumWindowHeight));
        SettingsStatusText.Text = "Настройки сброшены.";
    }

    private void AddStorageExclusion_Click(object sender, RoutedEventArgs e)
    {
        var value = SettingsExclusionPathBox.Text.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(value)) return;
        if (_storageExcludedPaths.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            SettingsStatusText.Text = "Такое исключение уже существует.";
            return;
        }
        _storageExcludedPaths.Add(value);
        StorageExclusionItems.Add(value);
        SettingsExclusionPathBox.Text = string.Empty;
        SaveStorageExclusions();
        _storageDrilldownCache.Clear();
        SettingsStatusText.Text = "Исключение добавлено.";
    }

    private void RemoveStorageExclusion_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsExclusionsList.SelectedItem is not string value) return;
        _storageExcludedPaths.RemoveAll(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
        StorageExclusionItems.Remove(value);
        SaveStorageExclusions();
        _storageDrilldownCache.Clear();
        SettingsStatusText.Text = "Исключение удалено.";
    }

    private async void ClearStorageExclusions_Click(object sender, RoutedEventArgs e)
    {
        if (_storageExcludedPaths.Count == 0) return;
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "Очистить исключения?",
            Content = "Storage Scanner снова сможет анализировать эти пути.",
            PrimaryButtonText = "Очистить",
            CloseButtonText = "Отмена"
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        _storageExcludedPaths.Clear();
        StorageExclusionItems.Clear();
        SaveStorageExclusions();
        _storageDrilldownCache.Clear();
        SettingsStatusText.Text = "Список исключений очищен.";
    }

    private async void RefreshDeveloperStorage_Click(object sender, RoutedEventArgs e) => await RefreshDeveloperStorageAsync();

    private async Task RefreshDeveloperStorageAsync()
    {
        if (IsDeveloperBusy) return;
        _developerStorageCts?.Cancel();
        _developerStorageCts?.Dispose();
        _developerStorageCts = new CancellationTokenSource();
        IsDeveloperBusy = true;
        DeveloperStorageStatus = "Анализ Developer Storage…";
        try
        {
            var result = await _developerStorageService.AnalyzeAsync(_developerStorageCts.Token);
            DockerStorageText = result.DockerSummary;
            DockerAvailable = result.DockerAvailable;
            WslStorageText = result.WslSummary;
            WslAvailable = result.WslAvailable;
            NpmStorageText = result.NpmSummary;
            NpmAvailable = result.NpmAvailable;
            NuGetStorageText = result.NuGetSummary;
            NuGetAvailable = result.NuGetAvailable;
            AndroidStorageText = result.AndroidSummary;
            AndroidAvailable = result.AndroidAvailable;
            _androidSdkPath = result.AndroidSdkPath;
            await RefreshAndroidPackagesAsync(_developerStorageCts.Token);
            DeveloperStorageStatus = $"Анализ завершён · обнаружено не менее {ByteSizeFormatter.Format(result.TotalDetectedBytes)} в WSL/npm/NuGet/Android. Docker показывает собственную статистику отдельно.";
        }
        catch (OperationCanceledException)
        {
            DeveloperStorageStatus = "Анализ Developer Storage отменён.";
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Developer Storage analysis failed.", ex);
            DeveloperStorageStatus = "Ошибка анализа: " + ex.Message;
        }
        finally
        {
            IsDeveloperBusy = false;
        }
    }

    private async void DockerBuildCache_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmDeveloperCleanupAsync("Очистить Docker build cache?", "Будет выполнено docker builder prune -f. Volumes и рабочие контейнеры не затрагиваются.")) return;
        await RunDeveloperCleanupAsync("Docker build cache", ct => _developerStorageService.CleanDockerBuildCacheAsync(ct));
    }

    private async void DockerImages_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmDeveloperCleanupAsync("Удалить dangling Docker images?", "Будет выполнено docker image prune -f. Используемые images и volumes не удаляются.")) return;
        await RunDeveloperCleanupAsync("Docker dangling images", ct => _developerStorageService.CleanDockerDanglingImagesAsync(ct));
    }

    private async void DockerContainers_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmDeveloperCleanupAsync("Удалить остановленные Docker containers?", "Будет выполнено docker container prune -f. Запущенные контейнеры и volumes не затрагиваются.")) return;
        await RunDeveloperCleanupAsync("Docker stopped containers", ct => _developerStorageService.CleanDockerStoppedContainersAsync(ct));
    }

    private async void NpmCache_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmDeveloperCleanupAsync("Очистить npm cache?", "Будет выполнено npm cache clean --force. Проекты и node_modules не удаляются.")) return;
        await RunDeveloperCleanupAsync("npm cache", ct => _developerStorageService.CleanNpmCacheAsync(ct));
    }

    private async void NuGetCache_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmDeveloperCleanupAsync("Очистить NuGet caches?", "Будет выполнено dotnet nuget locals all --clear. Пакеты при необходимости будут скачаны заново.")) return;
        await RunDeveloperCleanupAsync("NuGet caches", ct => _developerStorageService.CleanNuGetCachesAsync(ct));
    }

    private async Task RunDeveloperCleanupAsync(string name, Func<CancellationToken, Task<CommandResult>> action)
    {
        if (IsDeveloperBusy) return;
        _developerStorageCts?.Cancel();
        _developerStorageCts?.Dispose();
        _developerStorageCts = new CancellationTokenSource();
        IsDeveloperBusy = true;
        DeveloperStorageStatus = $"{name}: выполняется…";
        try
        {
            var result = await action(_developerStorageCts.Token);
            DeveloperStorageStatus = result.Success
                ? $"{name}: завершено. {FirstUsefulLine(result.Combined)}"
                : $"{name}: не выполнено. {FirstUsefulLine(result.Combined)}";
            await RefreshDeveloperStorageAfterOperationAsync();
        }
        catch (OperationCanceledException)
        {
            DeveloperStorageStatus = $"{name}: отменено.";
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"Developer cleanup failed: {name}.", ex);
            DeveloperStorageStatus = $"{name}: ошибка — {ex.Message}";
        }
        finally
        {
            IsDeveloperBusy = false;
        }
    }

    private async Task RefreshDeveloperStorageAfterOperationAsync()
    {
        try
        {
            var result = await _developerStorageService.AnalyzeAsync(_developerStorageCts?.Token ?? CancellationToken.None);
            DockerStorageText = result.DockerSummary; DockerAvailable = result.DockerAvailable;
            WslStorageText = result.WslSummary; WslAvailable = result.WslAvailable;
            NpmStorageText = result.NpmSummary; NpmAvailable = result.NpmAvailable;
            NuGetStorageText = result.NuGetSummary; NuGetAvailable = result.NuGetAvailable;
            AndroidStorageText = result.AndroidSummary; AndroidAvailable = result.AndroidAvailable; _androidSdkPath = result.AndroidSdkPath;
            await RefreshAndroidPackagesAsync(_developerStorageCts?.Token ?? CancellationToken.None);
        }
        catch (Exception ex) { DiagnosticLog.Write("Developer Storage refresh after cleanup failed.", ex); }
    }

    private async Task RefreshAndroidPackagesAsync(CancellationToken ct)
    {
        AndroidSdkPackageItems.Clear();
        if (!AndroidAvailable || string.IsNullOrWhiteSpace(_androidSdkPath)) return;
        try
        {
            var packages = await _developerStorageService.GetAndroidInstalledPackagesAsync(_androidSdkPath, ct);
            foreach (var package in packages) AndroidSdkPackageItems.Add(package);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Could not list Android SDK packages.", ex);
        }
    }

    private async Task<bool> ConfirmDeveloperCleanupAsync(string title, string content)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = "Продолжить",
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Close
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async void UninstallAndroidPackage_Click(object sender, RoutedEventArgs e)
    {
        if (AndroidPackageCombo.SelectedItem is not string packageId || string.IsNullOrWhiteSpace(_androidSdkPath))
        {
            DeveloperStorageStatus = "Выберите установленный Android SDK-пакет.";
            return;
        }
        if (!await ConfirmDeveloperCleanupAsync("Удалить Android SDK-пакет?", $"{packageId}\n\nУдаление выполнит штатный sdkmanager. Убедитесь, что пакет не нужен вашим проектам.")) return;
        await RunDeveloperCleanupAsync($"Android SDK: {packageId}", ct => _developerStorageService.UninstallAndroidPackageAsync(_androidSdkPath!, packageId, ct));
    }

    private void OpenAndroidSdk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_androidSdkPath) || !Directory.Exists(_androidSdkPath)) return;
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_androidSdkPath}\"") { UseShellExecute = true }); }
        catch (Exception ex) { DiagnosticLog.Write("Could not open Android SDK folder.", ex); DeveloperStorageStatus = "Не удалось открыть Android SDK: " + ex.Message; }
    }

    private void OpenWslSettings_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:optionalfeatures") { UseShellExecute = true }); }
        catch (Exception ex) { DiagnosticLog.Write("Could not open WSL settings.", ex); DeveloperStorageStatus = "Не удалось открыть компоненты Windows: " + ex.Message; }
    }

    private static string FirstUsefulLine(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Готово.";
        return value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? value.Trim();
    }

    private void NavigateToDisk_Click(object sender, RoutedEventArgs e) => SelectNavigationSection("disk");
    private void NavigateToDeep_Click(object sender, RoutedEventArgs e) => SelectNavigationSection("deep");

    private void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(DiagnosticLog.DirectoryPath);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{DiagnosticLog.DirectoryPath}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Could not open logs folder.", ex);
            StatusText = "Не удалось открыть папку логов: " + ex.Message;
        }
    }

    private void CopyDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = new StringBuilder()
                .AppendLine($"Safe C: Cleaner {AboutVersionText}")
                .AppendLine("Author: Cactus Dex")
                .AppendLine($"OS: {AboutDeviceText}")
                .AppendLine($"Machine: {AboutMachineName}")
                .AppendLine($"Process: {AboutHardwareText}")
                .AppendLine($"Runtime: {AboutDotNetText}")
                .AppendLine($"Windows App SDK assembly: {AboutWindowsAppSdkText}")
                .AppendLine($"Log: {DiagnosticLog.LogPath}")
                .ToString();
            var data = new DataPackage();
            data.SetText(text);
            Clipboard.SetContent(data);
            StatusText = "Сведения о программе скопированы.";
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Could not copy diagnostics.", ex);
            StatusText = "Не удалось скопировать сведения.";
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        SaveWindowState();
        RemoveTrayIcon();
    }

    private void SaveWindowState()
    {
        try
        {
            _settings.IsNavigationPaneOpen = RootNavigation.IsPaneOpen;
            _settings.WindowWidth = Math.Max(MinimumWindowWidth, AppWindow.Size.Width);
            _settings.WindowHeight = Math.Max(MinimumWindowHeight, AppWindow.Size.Height);
            _settings.WindowX = AppWindow.Position.X;
            _settings.WindowY = AppWindow.Position.Y;
            _settingsService.Save(_settings);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Could not save window state.", ex);
        }
    }

    private void RestoreWindowPosition(int width, int height)
    {
        if (_settings.WindowX is not int x || _settings.WindowY is not int y) return;
        var rect = new NativeRect { Left = x, Top = y, Right = x + width, Bottom = y + height };
        if (MonitorFromRect(ref rect, 0) == IntPtr.Zero) return;
        AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
    }

    private static string StorageExclusionsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SafeCCleaner", "storage-exclusions.json");

    private void LoadStorageExclusions()
    {
        try
        {
            if (!File.Exists(StorageExclusionsPath)) return;
            var values = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(StorageExclusionsPath)) ?? [];
            _storageExcludedPaths.Clear();
            _storageExcludedPaths.AddRange(values.Where(x => !string.IsNullOrWhiteSpace(x)));
            StorageExclusionItems.Clear();
            foreach (var value in _storageExcludedPaths) StorageExclusionItems.Add(value);
        }
        catch (Exception ex) { DiagnosticLog.Write("Could not load storage exclusions.", ex); }
    }

    private void SaveStorageExclusions()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorageExclusionsPath)!);
            File.WriteAllText(StorageExclusionsPath, JsonSerializer.Serialize(_storageExcludedPaths, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { DiagnosticLog.Write("Could not save storage exclusions.", ex); }
    }

    private async void AnalyzeStorage_Click(object sender, RoutedEventArgs e)
    {
        if (!CanStartAnalyzer) return;

        _analyzerCts?.Cancel();
        _analyzerCts = new CancellationTokenSource();
        var token = _analyzerCts.Token;
        var root = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
        var previousSummary = AnalyzerSummaryText;
        var previousLastRun = AnalyzerLastRunText;

        try
        {
            IsAnalyzerScanning = true;
            AnalyzerProgressPercent = 0;
            AnalyzerProgressText = "Подготовка…";
            AnalyzerStatusText = "Индексирую диск. Reparse points и исключения Storage Scanner пропускаются.";
            AnalyzerSummaryText = "Анализ C: выполняется…";
            _analyzerStartedAt = DateTimeOffset.Now;

            var progress = new Progress<StorageAnalyzerProgress>(p =>
            {
                AnalyzerProgressPercent = Math.Clamp(p.ApproximatePercent, 0, 100);
                var elapsed = DateTimeOffset.Now - _analyzerStartedAt;
                var etaText = string.Empty;
                if (p.ApproximatePercent >= 5 && elapsed.TotalSeconds >= 3)
                {
                    var total = elapsed.TotalSeconds / Math.Max(0.01, p.ApproximatePercent / 100d);
                    etaText = " · " + FormatEta(TimeSpan.FromSeconds(Math.Max(0, total - elapsed.TotalSeconds)));
                }
                AnalyzerProgressText = $"{p.ApproximatePercent:0}% · {p.FilesScanned:N0} файлов{etaText}";
                var current = p.CurrentPath.Length > 110 ? "…" + p.CurrentPath[^107..] : p.CurrentPath;
                AnalyzerStatusText = $"Сканирование: {current}" + (p.AccessLimitedCount > 0 ? $" · ограничений доступа: {p.AccessLimitedCount:N0}" : string.Empty);
            });

            var result = await _storageAnalyzerService.AnalyzeAsync(root, _storageExcludedPaths, progress, token);
            token.ThrowIfCancellationRequested();
            _analyzerFiles = NormalizeAnalyzerFileCategories(result.LargestFiles);
            _analyzerFolders = result.LargestDirectories;
            var capturedAt = DateTimeOffset.Now;
            _storageAnalyzerCacheService.Save(result, capturedAt);
            _hasAnalyzerCache = true;
            OnPropertyChanged(nameof(AnalyzerScanButtonText));
            ApplyAnalyzerFilters();

            AnalyzerProgressPercent = 100;
            AnalyzerProgressText = $"Готово · {FormatDuration(result.Duration)}";
            AnalyzerLastRunText = $"Последний анализ: {capturedAt.LocalDateTime:dd.MM.yyyy HH:mm} · сохранено";
            AnalyzerSummaryText = $"Просканировано {ByteSizeFormatter.Format(result.ScannedBytes)} · {result.FileCount:N0} файлов · {result.DirectoryCount:N0} папок";
            AnalyzerStatusText = result.AccessLimitedCount > 0
                ? $"Готово. Недоступных участков: {result.AccessLimitedCount:N0}; результаты по ним могут быть неполными."
                : "Готово. Анализатор ничего не изменял на диске.";
        }
        catch (OperationCanceledException)
        {
            if (_hasAnalyzerCache)
            {
                AnalyzerSummaryText = previousSummary;
                AnalyzerLastRunText = previousLastRun;
            }
            AnalyzerStatusText = _hasAnalyzerCache
                ? "Анализ отменён. На экране оставлены предыдущие сохранённые результаты."
                : "Анализ отменён.";
            AnalyzerProgressText = "Отменено";
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Storage Analyzer failed.", ex);
            if (_hasAnalyzerCache)
            {
                AnalyzerSummaryText = previousSummary;
                AnalyzerLastRunText = previousLastRun;
            }
            AnalyzerStatusText = "Ошибка анализа: " + ex.Message + (_hasAnalyzerCache ? " · показаны предыдущие результаты." : string.Empty);
            AnalyzerProgressText = "Ошибка";
        }
        finally
        {
            IsAnalyzerScanning = false;
        }
    }

    private void CancelAnalyzer_Click(object sender, RoutedEventArgs e) => _analyzerCts?.Cancel();

    private void AnalyzerFilter_Changed(object sender, SelectionChangedEventArgs e) => ApplyAnalyzerFilters();

    private void ApplyAnalyzerFilters()
    {
        if (AnalyzerMinSizeCombo is null || AnalyzerCategoryCombo is null ||
            AnalyzerAgeCombo is null || AnalyzerSortCombo is null)
            return;

        const int resultLimit = 50;
        var minBytes = ParseLongTag(AnalyzerMinSizeCombo, 0);
        var category = GetSelectedTag(AnalyzerCategoryCombo, "all");
        var ageDays = (int)ParseLongTag(AnalyzerAgeCombo, 0);
        var sort = GetSelectedTag(AnalyzerSortCombo, "size");
        var cutoff = ageDays > 0 ? DateTimeOffset.UtcNow.AddDays(-ageDays) : DateTimeOffset.MaxValue;

        IEnumerable<StorageAnalyzerEntry> files = _analyzerFiles.Where(x => x.Bytes >= minBytes);
        IEnumerable<StorageAnalyzerEntry> folders = _analyzerFolders.Where(x => x.Bytes >= minBytes);

        if (!string.Equals(category, "all", StringComparison.OrdinalIgnoreCase))
            files = files.Where(x => string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase));
        if (ageDays > 0)
            files = files.Where(x => x.LastWriteTime != DateTimeOffset.MinValue && x.LastWriteTime <= cutoff);

        files = SortAnalyzerEntries(files, sort).Take(resultLimit);
        folders = SortAnalyzerEntries(folders, sort).Take(resultLimit);

        AnalyzerFileItems.Clear();
        foreach (var item in files) AnalyzerFileItems.Add(new StorageAnalyzerItemViewModel(item));
        AnalyzerFolderItems.Clear();
        foreach (var item in folders) AnalyzerFolderItems.Add(new StorageAnalyzerItemViewModel(item));
    }

    private static IReadOnlyList<StorageAnalyzerEntry> NormalizeAnalyzerFileCategories(IEnumerable<StorageAnalyzerEntry> entries) =>
        entries.Select(entry => entry with
        {
            Category = StorageAnalyzerService.CategorizeFile(Path.GetExtension(entry.Path))
        }).ToArray();

    private static IEnumerable<StorageAnalyzerEntry> SortAnalyzerEntries(IEnumerable<StorageAnalyzerEntry> source, string sort) => sort switch
    {
        "date" => source.OrderBy(x => x.LastWriteTime == DateTimeOffset.MinValue ? DateTimeOffset.MaxValue : x.LastWriteTime).ThenByDescending(x => x.Bytes),
        "name" => source.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase),
        _ => source.OrderByDescending(x => x.Bytes).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
    };

    private static long ParseLongTag(ComboBox combo, long fallback) =>
        combo.SelectedItem is ComboBoxItem item && long.TryParse(item.Tag?.ToString(), out var value) ? value : fallback;

    private static string GetSelectedTag(ComboBox combo, string fallback) =>
        combo.SelectedItem is ComboBoxItem item && !string.IsNullOrWhiteSpace(item.Tag?.ToString()) ? item.Tag!.ToString()! : fallback;

    private void OpenAnalyzerItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string path } || string.IsNullOrWhiteSpace(path)) return;
        try
        {
            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
                AnalyzerStatusText = "Открыта папка: " + path;
            }
            else if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
                AnalyzerStatusText = "Файл выделен в Проводнике: " + path;
            }
            else
            {
                AnalyzerStatusText = "Объект больше не существует.";
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Could not open Analyzer item.", ex);
            AnalyzerStatusText = "Не удалось открыть расположение: " + ex.Message;
        }
    }

    private async void AnalyzeAnalyzerFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string path } || string.IsNullOrWhiteSpace(path)) return;
        SelectNavigationSection("disk");
        await AnalyzeFolderAsync(path);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string path } || string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            if (!Directory.Exists(path))
            {
                StatusText = "Каталог больше не существует.";
                return;
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            StatusText = "Открыто расположение: " + path;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Could not open folder in Explorer.", ex);
            StatusText = "Не удалось открыть расположение: " + ex.Message;
        }
    }

    private async void HistoryDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is null || !long.TryParse(button.Tag.ToString(), out var id))
            return;

        var vm = CleanupHistoryItems.FirstOrDefault(x => x.Id == id);
        if (vm is null)
            return;

        var record = vm.Record;
        var details = new StringBuilder();
        details.AppendLine($"{record.CompletedAt.LocalDateTime:dd.MM.yyyy HH:mm:ss} · {record.Mode}");
        details.AppendLine($"Свободно: {ByteSizeFormatter.Format(record.BeforeFreeBytes)} → {ByteSizeFormatter.Format(record.AfterFreeBytes)}");
        details.AppendLine($"Фактическое изменение: {FormatSignedBytes(record.AfterFreeBytes - record.BeforeFreeBytes)}");
        details.AppendLine($"По правилам: {ByteSizeFormatter.Format(record.ReportedFreedBytes)} · пропущено файлов: {record.SkippedFiles}");
        if (!string.IsNullOrWhiteSpace(record.Summary))
            details.AppendLine().AppendLine(record.Summary);

        if (record.Items.Count > 0)
        {
            details.AppendLine().AppendLine("Категории:");
            foreach (var item in record.Items)
            {
                details.Append("• ").Append(item.DisplayName)
                    .Append(" · ").Append(item.Success ? "OK" : "ошибка")
                    .Append(" · ").Append(ByteSizeFormatter.Format(item.FreedBytes))
                    .Append(" · удалено ").Append(item.DeletedFiles)
                    .Append(" · пропущено ").Append(item.SkippedFiles);
                if (!string.IsNullOrWhiteSpace(item.Message))
                    details.Append(" · ").Append(item.Message);
                details.AppendLine();
            }
        }

        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = record.OperationName,
            Content = new ScrollViewer
            {
                MaxHeight = 520,
                Content = new TextBlock { Text = details.ToString(), TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true }
            },
            CloseButtonText = "Закрыть"
        };
        await dialog.ShowAsync();
    }

    private async Task RefreshHistoryAsync()
    {
        try
        {
            var records = await _repository.GetCleanupRunsAsync(10);
            CleanupHistoryItems.Clear();
            foreach (var record in records)
                CleanupHistoryItems.Add(new CleanupHistoryViewModel(record));

            if (records.Count > 0)
            {
                var latest = records[0];
                var delta = latest.AfterFreeBytes - latest.BeforeFreeBytes;
                LastCleanupSummary = latest.Success
                    ? (delta >= 0
                        ? $"+{ByteSizeFormatter.Format(delta)} свободного места"
                        : $"{ByteSizeFormatter.Format(Math.Abs(delta))} занято параллельно")
                    : "Последняя операция завершилась с ошибками";
                LastCleanupDetail = $"{latest.OperationName} · {latest.CompletedAt.LocalDateTime:dd.MM.yyyy HH:mm} · " +
                                    $"свободно {ByteSizeFormatter.Format(latest.BeforeFreeBytes)} → {ByteSizeFormatter.Format(latest.AfterFreeBytes)}";
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Cleanup history refresh failed.", ex);
            StatusText = "Не удалось загрузить историю очисток: " + ex.Message;
        }
    }

    private async Task SaveCleanupHistoryAsync(CleanupHistoryRecord record)
    {
        try
        {
            await _repository.SaveCleanupRunAsync(record);
            await RefreshHistoryAsync();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Cleanup history save failed.", ex);
            StatusText = "Очистка выполнена, но запись истории не сохранилась: " + ex.Message;
        }
    }

    private async Task RecordSystemOperationAsync(
        string operationName,
        string mode,
        CleanupRisk risk,
        DateTimeOffset startedAt,
        long beforeFree,
        long afterFree,
        SystemOperationResult result)
    {
        var item = new CleanupHistoryItem(
            result.OperationId,
            operationName,
            risk,
            result.Success,
            result.FreedBytes,
            0,
            0,
            result.Message);

        var record = new CleanupHistoryRecord(
            0,
            startedAt,
            DateTimeOffset.Now,
            operationName,
            mode,
            result.Success,
            beforeFree,
            afterFree,
            result.FreedBytes,
            0,
            result.Details ?? result.Message,
            [item]);

        await SaveCleanupHistoryAsync(record);
        await ShowCleanupResultDialogAsync(record);
    }

    private async Task ShowCleanupResultDialogAsync(CleanupHistoryRecord record)
    {
        var delta = record.AfterFreeBytes - record.BeforeFreeBytes;
        var deltaText = delta >= 0
            ? $"+{ByteSizeFormatter.Format(delta)} свободного места"
            : $"{ByteSizeFormatter.Format(Math.Abs(delta))} занято параллельно";

        var content = new StackPanel { Spacing = 10, MinWidth = 430 };
        content.Children.Add(new TextBlock
        {
            Text = deltaText,
            FontSize = 26,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Text = $"Свободно: {ByteSizeFormatter.Format(record.BeforeFreeBytes)} → {ByteSizeFormatter.Format(record.AfterFreeBytes)}",
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = $"Удалено по правилам: {ByteSizeFormatter.Format(record.ReportedFreedBytes)}" +
                   (record.SkippedFiles > 0 ? $" · пропущено: {record.SkippedFiles:N0}" : string.Empty),
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap
        });

        foreach (var item in record.Items)
        {
            var row = new Grid { ColumnSpacing = 12, Margin = new Thickness(0, 5, 0, 5) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel { Spacing = 2 };
            left.Children.Add(new TextBlock
            {
                Text = item.DisplayName,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            left.Children.Add(new TextBlock
            {
                Text = item.Success
                    ? $"Удалено файлов: {item.DeletedFiles:N0}" + (item.SkippedFiles > 0 ? $" · пропущено: {item.SkippedFiles:N0}" : string.Empty)
                    : "Не выполнено: " + (item.Message ?? "неизвестная ошибка"),
                Opacity = 0.62,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });

            var right = new TextBlock
            {
                Text = item.Success ? ByteSizeFormatter.Format(item.FreedBytes) : "ошибка",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(right, 1);
            row.Children.Add(left);
            row.Children.Add(right);
            content.Children.Add(row);
        }

        var dialog = new ContentDialog
        {
            Title = record.Success ? "Очистка завершена" : "Операция завершилась с ошибками",
            Content = new ScrollViewer { Content = content, MaxHeight = 520 },
            PrimaryButtonText = "Готово",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RootGrid.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void ExportHistory_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var records = await _repository.GetCleanupRunsAsync(10);
            if (records.Count == 0)
            {
                StatusText = "История очисток пока пуста — экспортировать нечего.";
                return;
            }

            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(documents))
                documents = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SafeCCleaner");
            var folder = Path.Combine(documents, "SafeCCleaner Reports");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"SafeCCleaner-report-{DateTime.Now:yyyyMMdd-HHmmss}.txt");

            var text = BuildHistoryReport(records);
            await File.WriteAllTextAsync(path, text, new UTF8Encoding(true));
            StatusText = "Отчёт сохранён: " + path;

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("History export failed.", ex);
            StatusText = "Не удалось экспортировать отчёт: " + ex.Message;
        }
    }

    private static string BuildHistoryReport(IReadOnlyList<CleanupHistoryRecord> records)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Safe C: Cleaner — история очисток");
        sb.AppendLine($"Экспорт: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
        sb.AppendLine(new string('=', 72));

        foreach (var record in records)
        {
            var delta = record.AfterFreeBytes - record.BeforeFreeBytes;
            sb.AppendLine();
            sb.AppendLine($"[{record.CompletedAt.LocalDateTime:dd.MM.yyyy HH:mm:ss}] {record.OperationName} · {record.Mode}");
            sb.AppendLine($"Результат: {(record.Success ? "успешно" : "с ошибками")}");
            sb.AppendLine($"Свободно: {ByteSizeFormatter.Format(record.BeforeFreeBytes)} -> {ByteSizeFormatter.Format(record.AfterFreeBytes)}");
            sb.AppendLine($"Фактическое изменение: {(delta >= 0 ? "+" : "-")}{ByteSizeFormatter.Format(Math.Abs(delta))}");
            sb.AppendLine($"Удалено по правилам: {ByteSizeFormatter.Format(record.ReportedFreedBytes)}; пропущено: {record.SkippedFiles:N0}");

            foreach (var item in record.Items)
            {
                sb.Append("  - ").Append(item.DisplayName).Append(": ")
                    .Append(item.Success ? ByteSizeFormatter.Format(item.FreedBytes) : "ОШИБКА")
                    .Append($"; удалено файлов {item.DeletedFiles:N0}; пропущено {item.SkippedFiles:N0}");
                if (!string.IsNullOrWhiteSpace(item.Message))
                    sb.Append("; ").Append(item.Message);
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private async Task<bool> ConfirmIfNeededAsync(CleanupItemViewModel item)
    {
        if (item.Rule.Risk == CleanupRisk.Safe)
            return true;

        var title = item.Id switch
        {
            "recycle-bin" => "Очистить корзину?",
            "system-crash-dumps" => "Удалить системные дампы Windows?",
            _ => $"Подтвердить: {item.Name}"
        };

        var message = item.Id switch
        {
            "recycle-bin" => "Это пользовательские удалённые файлы. После очистки корзины штатное восстановление будет недоступно.",
            "system-crash-dumps" => "MEMORY.DMP и Minidump используются для анализа BSOD и критических сбоев. После удаления существующие дампы восстановить штатно нельзя.",
            _ => "Эта категория может быть полезна для диагностики или восстановления и поэтому требует явного подтверждения."
        };

        return await ConfirmSystemActionAsync(title, message, "Продолжить");
    }

    private async Task<bool> ConfirmSystemActionAsync(string title, string content, string primaryText)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = primaryText,
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = RootGrid.XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void UpdateReclaimableTotals()
    {
        var safeBytes = CleanupItems
            .Where(x => x.Rule.Risk == CleanupRisk.Safe)
            .Sum(x => x.ReclaimableBytes);
        SafeReclaimableText = ByteSizeFormatter.Format(safeBytes);
    }

    private static long GetSystemDriveFreeBytes()
    {
        try
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var root = Path.GetPathRoot(windows) ?? "C:\\";
            return new DriveInfo(root).AvailableFreeSpace;
        }
        catch
        {
            return 0;
        }
    }

    private void UpdateCleanupReport(
        string operationName,
        long beforeFree,
        long afterFree,
        long reportedFreed,
        long skipped)
    {
        var observedDelta = afterFree - beforeFree;
        LastCleanupSummary = observedDelta >= 0
            ? $"+{ByteSizeFormatter.Format(observedDelta)} свободного места"
            : $"{ByteSizeFormatter.Format(Math.Abs(observedDelta))} занято параллельно";

        LastCleanupDetail = $"{operationName} · свободно {ByteSizeFormatter.Format(beforeFree)} → {ByteSizeFormatter.Format(afterFree)} · " +
                            $"удалено по правилам {ByteSizeFormatter.Format(reportedFreed)}" +
                            (skipped > 0 ? $" · пропущено {skipped:N0}" : string.Empty);
    }

    private void UpdateSystemOperationReport(
        string operationName,
        long beforeFree,
        long afterFree,
        SystemOperationResult result)
    {
        if (!result.Success)
        {
            LastCleanupSummary = "Операция не завершена";
            LastCleanupDetail = $"{operationName}: {result.Message}";
            return;
        }

        UpdateCleanupReport(operationName, beforeFree, afterFree, result.FreedBytes, 0);
    }

    private async void ExportHistoryJson_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var records = await _repository.GetCleanupRunsAsync(10);
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var directory = Path.Combine(documents, "SafeCCleaner Reports");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"cleanup-history-{DateTime.Now:yyyyMMdd-HHmmss}.json");

            var export = records.Select(record => new
            {
                record.Id,
                record.StartedAt,
                record.CompletedAt,
                record.OperationName,
                record.Mode,
                record.Success,
                record.BeforeFreeBytes,
                record.AfterFreeBytes,
                ActualDeltaBytes = record.AfterFreeBytes - record.BeforeFreeBytes,
                record.ReportedFreedBytes,
                record.SkippedFiles,
                record.Summary,
                Items = record.Items.Select(item => new
                {
                    item.RuleId,
                    item.DisplayName,
                    Risk = item.Risk.ToString(),
                    item.Success,
                    item.FreedBytes,
                    item.DeletedFiles,
                    item.SkippedFiles,
                    item.Message
                })
            });

            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true }));
            StatusText = "JSON-отчёт сохранён: " + path;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("JSON history export failed.", ex);
            StatusText = "Не удалось экспортировать JSON: " + ex.Message;
        }
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
