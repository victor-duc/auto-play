using System.IO;
using System.Windows;
using System.Windows.Threading;
using AutoPlay.App.Services;
using AutoPlay.App.ViewModels;
using AutoPlay.App.Views;
using AutoPlay.Core.Abstractions;
using AutoPlay.Core.Execution;
using AutoPlay.Core.Storage;
using AutoPlay.Vision;
using AutoPlay.Windows.Capture;
using AutoPlay.Windows.Input;
using AutoPlay.Windows.Power;
using Microsoft.Extensions.DependencyInjection;

namespace AutoPlay.App;

public partial class App : Application
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

        // Platform services
        services.AddSingleton<IScreenCapture, GdiScreenCapture>();
        services.AddSingleton<IInputDriver, SendInputDriver>();
        services.AddSingleton<IPowerManager, WindowsPowerManager>();
        services.AddSingleton<ITemplateMatcher, OpenCvTemplateMatcher>();
        services.AddSingleton<IImageCodec, OpenCvImageCodec>();
        services.AddSingleton<IClock>(SystemClock.Instance);
        services.AddSingleton<IProfileStore>(sp => new FileProfileStore(DefaultProfilesPath, sp.GetRequiredService<IImageCodec>()));
        services.AddTransient(_ => new Random());
        services.AddTransient<SequenceRunner>();

        // UI
        services.AddSingleton<IUserDialogs, MessageBoxDialogs>();
        services.AddSingleton<ScreenRecordingFlow>();
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
