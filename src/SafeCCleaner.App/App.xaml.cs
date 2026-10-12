using Microsoft.UI.Xaml;
using SafeCCleaner.App.Services;

namespace SafeCCleaner.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += App_UnhandledException;
        DiagnosticLog.Write("Application object initialized.");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            DiagnosticLog.Write("OnLaunched entered.");

            if (!SingleInstanceService.TryAcquire())
            {
                var backgroundLaunch = Environment.GetCommandLineArgs()
                    .Any(x => string.Equals(x, "--background", StringComparison.OrdinalIgnoreCase));

                if (!backgroundLaunch)
                    SingleInstanceService.SignalExistingInstance();

                DiagnosticLog.Write("Existing instance detected; secondary process is exiting.");
                Environment.Exit(0);
                return;
            }

            _window = new MainWindow();
            _window.Activate();
            DiagnosticLog.Write("Main window activated.");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Fatal exception while creating/activating MainWindow.", ex);
            DiagnosticLog.OpenInNotepad();
            Environment.Exit(1);
        }
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        DiagnosticLog.Write("Unhandled WinUI exception.", e.Exception);
        DiagnosticLog.OpenInNotepad();
        // Do not mark as handled: continuing after an unknown UI failure can be unsafe.
    }
}
