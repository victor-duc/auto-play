using System.IO;
using System.Windows;
using System.Windows.Threading;
using AutoPlay.App.Services;
using AutoPlay.Presentation.Services;
using AutoPlay.Presentation.ViewModels;
using AutoPlay.App.Views;
using AutoPlay.Adapters.Persistence;
using AutoPlay.Application.Ports;
using AutoPlay.Application.UseCases;
using AutoPlay.Application.Execution;
using AutoPlay.Application.Settings;
using AutoPlay.Adapters.Vision;
using AutoPlay.Adapters.Windows.Capture;
using AutoPlay.Adapters.Windows.Input;
using AutoPlay.Adapters.Windows.Power;
using AutoPlay.Adapters.Windows.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace AutoPlay.App;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Disposed in OnExit, at the end of the application lifetime.")]
public partial class App : System.Windows.Application
{
    /// <summary>Name of the mutex that prevents two instances from writing the same files.</summary>
    private const string SingleInstanceMutexName = @"Local\AutoPlay.SingleInstance";

    private ServiceProvider? _services;
    private Mutex? _singleInstanceMutex;

    private Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("AutoPlay is already running.", "AutoPlay", MessageBoxButton.OK, MessageBoxImage.Information);
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        // The settings are read first: they choose the data folder and the theme.
        var settingsService = new SettingsService(new JsonSettingsStore(Path.Combine(AppPaths.DefaultRoot, "settings.json")));
        var paths = new AppPaths(AppPaths.DefaultRoot, settingsService.Current.DataFolder);
        ApplyTheme(settingsService.Current.Theme);
        settingsService.Changed += (_, settings) => ApplyTheme(settings.Theme);

        _services = ConfigureServices(paths, settingsService).BuildServiceProvider();
        _logger = _services.GetRequiredService<ILoggerFactory>().CreateLogger("AutoPlay");
        var version = typeof(App).Assembly.GetName().Version?.ToString() ?? "?";
        AppLog.Started(_logger, version, paths.Root);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Crashed(_logger, args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Unexpected(_logger, args.Exception);
            args.SetObserved();
        };

        var mainWindow = _services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
        StartScheduler();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLog.Stopped(_logger);
        _services?.Dispose();
        if (_singleInstanceMutex is not null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
    }

    private static ServiceCollection ConfigureServices(AppPaths paths, SettingsService settingsService)
    {
        var services = new ServiceCollection();

        // Logging: rolling files in %LOCALAPPDATA%\AutoPlay\Logs, one per day, kept 14 days.
        var fileLogger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(paths.Logs, "autoplay-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                formatProvider: System.Globalization.CultureInfo.InvariantCulture,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        services.AddLogging(builder => builder.AddSerilog(fileLogger, dispose: true));
        services.AddSingleton(paths);
        services.AddSingleton(settingsService);
        services.AddSingleton<IShellLauncher, ShellLauncher>();

        // Driven adapters (implement the application ports)
        services.AddSingleton<IScreenCapture, GdiScreenCapture>();
        services.AddSingleton<IInputDriver, SendInputDriver>();
        services.AddSingleton<IPowerManager, WindowsPowerManager>();
        services.AddSingleton<ITemplateMatcher, OpenCvTemplateMatcher>();
        services.AddSingleton<IClock>(SystemClock.Instance);
        services.AddSingleton(_ => new FileProfileStore(paths.Profiles, new OpenCvImageCodec()));
        services.AddSingleton<IProfileRepository>(sp => sp.GetRequiredService<FileProfileStore>());
        services.AddSingleton<IScreenRepository>(sp => sp.GetRequiredService<FileProfileStore>());
        services.AddSingleton<ISequenceRepository>(sp => sp.GetRequiredService<FileProfileStore>());
        services.AddSingleton<IRunLogStore>(sp => sp.GetRequiredService<FileProfileStore>());
        services.AddSingleton<IProfileArchive>(sp => sp.GetRequiredService<FileProfileStore>());
        services.AddTransient(_ => new Random());

        // Application (use cases)
        services.AddSingleton<ProfileService>();
        services.AddSingleton<ScreenService>();
        services.AddSingleton<SequenceService>();
        services.AddSingleton<SequenceExecutionService>();
        services.AddSingleton<ScheduleService>();
        services.AddTransient<SequenceRunner>();

        // Driving adapter (WPF user interface)
        services.AddSingleton<IUserDialogs, MessageBoxDialogs>();
        services.AddSingleton<ActivityTracker>();
        services.AddSingleton(sp => new RunScheduler(
            sp.GetRequiredService<ScheduleService>(),
            sp.GetRequiredService<ISequenceExecutionFlow>(),
            sp.GetRequiredService<ActivityTracker>(),
            () => DateTime.Now));
        services.AddSingleton<IScreenRecordingFlow, ScreenRecordingFlow>();
        services.AddSingleton<ISettingsFlow, SettingsFlow>();
        services.AddSingleton<ISequenceEditingFlow, SequenceEditingFlow>();
        services.AddSingleton<ISequenceExecutionFlow, SequenceExecutionFlow>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services;
    }

#pragma warning disable WPF0001 // The Fluent theme is flagged as experimental.
    private void ApplyTheme(ThemePreference theme) => ThemeMode = theme switch
    {
        ThemePreference.Light => ThemeMode.Light,
        ThemePreference.Dark => ThemeMode.Dark,
        _ => ThemeMode.System,
    };
#pragma warning restore WPF0001

    /// <summary>Checks the scheduled sequences every 10 seconds while AutoPlay is running.</summary>
    private void StartScheduler()
    {
        var scheduler = _services!.GetRequiredService<RunScheduler>();
        scheduler.Notification += (_, message) => AppLog.Scheduler(_logger, message);
        _services!.GetRequiredService<MainViewModel>().AttachScheduler();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        timer.Tick += async (_, _) => await scheduler.CheckAsync();
        timer.Start();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Unexpected(_logger, e.Exception);
        MessageBox.Show(MainWindow, e.Exception.Message, "AutoPlay — Unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
