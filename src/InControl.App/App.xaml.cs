using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using InControl.App.Services;

namespace InControl.App;

/// <summary>
/// Application entry point and DI container host.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    public App()
    {
        InitializeComponent();

        // Set up global exception handling
        UnhandledException += OnUnhandledException;
        System.AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <summary>
    /// Gets the application's service provider.
    /// </summary>
    public static IServiceProvider Services => ((App)Current)._host!.Services;

    /// <summary>
    /// Gets a service from the DI container.
    /// </summary>
    public static T GetService<T>() where T : class => Services.GetRequiredService<T>();

    /// <summary>
    /// The main window.
    /// </summary>
    public static MainWindow? MainWindow { get; private set; }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            // Check for crash recovery BEFORE initializing
            CrashRecoveryService.Instance.CheckForCrashRecovery();

            // Set crash marker (cleared on clean exit)
            CrashRecoveryService.Instance.SetCrashMarker();

            // Initialize DI container after XAML framework is fully initialized
            _host = CreateHostBuilder().Build();
            await _host.StartAsync();

            MainWindow = new MainWindow();
            MainWindow.Closed += OnMainWindowClosed;
            MainWindow.Activate();
        }
        catch (Exception ex)
        {
            // Log fatal error
            LogFatalError(ex);
            throw;
        }
    }

    private void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        // Clean shutdown - clear crash marker
        CrashRecoveryService.Instance.ClearCrashMarker();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        LogFatalError(e.Exception);
        // Don't mark as handled - let the app crash but with logging
    }

    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogFatalError(ex);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogFatalError(e.Exception);
        e.SetObserved(); // Prevent app crash from unobserved task exceptions
    }

    private static void LogFatalError(Exception ex)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"FATAL: {ex}");
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "InControl", "Logs", "crash.log");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);

            var crashInfo = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] FATAL CRASH\n" +
                           $"Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}\n" +
                           $"Exception: {ex.GetType().FullName}\n" +
                           $"Message: {ex.Message}\n" +
                           $"StackTrace:\n{ex.StackTrace}\n";

            if (ex.InnerException != null)
            {
                crashInfo += $"\nInner Exception: {ex.InnerException.GetType().FullName}\n" +
                            $"Inner Message: {ex.InnerException.Message}\n" +
                            $"Inner StackTrace:\n{ex.InnerException.StackTrace}\n";
            }

            System.IO.File.WriteAllText(logPath, crashInfo);
        }
        catch
        {
            // Can't do much if logging fails
        }
    }

    private static IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Configuration is set up in Startup
                Startup.ConfigureServices(services, context.Configuration);
            });
    }
}
