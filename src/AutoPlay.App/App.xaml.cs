using System.IO;
using System.Windows;
using System.Windows.Threading;
using AutoPlay.App.Services;
using AutoPlay.App.ViewModels;
using AutoPlay.App.Views;
using AutoPlay.Adapters.Persistence;
using AutoPlay.Application.Ports;
using AutoPlay.Application.UseCases;
using AutoPlay.Application.Execution;
using AutoPlay.Adapters.Vision;
using AutoPlay.Adapters.Windows.Capture;
using AutoPlay.Adapters.Windows.Input;
using AutoPlay.Adapters.Windows.Power;
using AutoPlay.Adapters.Windows.Time;
using Microsoft.Extensions.DependencyInjection;

namespace AutoPlay.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;

    /// <summary>Default root folder of the profiles: %LOCALAPPDATA%\AutoPlay\Profiles.</summary>
    public static string DefaultProfilesPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoPlay", "Profiles");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        _services = ConfigureServices().BuildServiceProvider();
        var mainWindow = _services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private static ServiceCollection ConfigureServices()
    {
        var services = new ServiceCollection();

        // Driven adapters (implement the application ports)
        services.AddSingleton<IScreenCapture, GdiScreenCapture>();
        services.AddSingleton<IInputDriver, SendInputDriver>();
        services.AddSingleton<IPowerManager, WindowsPowerManager>();
        services.AddSingleton<ITemplateMatcher, OpenCvTemplateMatcher>();
        services.AddSingleton<IClock>(SystemClock.Instance);
        services.AddSingleton(_ => new FileProfileStore(DefaultProfilesPath, new OpenCvImageCodec()));
        services.AddSingleton<IProfileRepository>(sp => sp.GetRequiredService<FileProfileStore>());
        services.AddSingleton<IScreenRepository>(sp => sp.GetRequiredService<FileProfileStore>());
        services.AddSingleton<ISequenceRepository>(sp => sp.GetRequiredService<FileProfileStore>());
        services.AddSingleton<IRunLogStore>(sp => sp.GetRequiredService<FileProfileStore>());
        services.AddTransient(_ => new Random());

        // Application (use cases)
        services.AddSingleton<ProfileService>();
        services.AddSingleton<ScreenService>();
        services.AddSingleton<SequenceService>();
        services.AddSingleton<SequenceExecutionService>();
        services.AddTransient<SequenceRunner>();

        // Driving adapter (WPF user interface)
        services.AddSingleton<IUserDialogs, MessageBoxDialogs>();
        services.AddSingleton<ScreenRecordingFlow>();
        services.AddSingleton<SequenceEditingFlow>();
        services.AddSingleton<SequenceExecutionFlow>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(MainWindow, e.Exception.Message, "AutoPlay — Unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
