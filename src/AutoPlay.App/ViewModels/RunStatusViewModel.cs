using System.Diagnostics;
using System.Globalization;
using System.Windows.Threading;
using AutoPlay.Application.Execution;
using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Model;
using AutoPlay.Application.Ports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoPlay.App.ViewModels;

public enum RunState
{
    Running,
    Paused,
    Stopped,
    Completed,
    Failed,
}

/// <summary>Runs a sequence and exposes its progress, with stop, resume and close actions.</summary>
public sealed partial class RunStatusViewModel : ObservableObject
{
    private readonly SequenceRunner _runner;
    private readonly IProfileStore _store;
    private readonly Profile _profile;
    private readonly Sequence _sequence;
    private readonly IReadOnlyDictionary<Guid, ScreenAssets> _screens;
    private readonly PixelRect _targetRegion;
    private readonly Stopwatch _elapsed = new();
    private readonly DispatcherTimer _timer;
    private CancellationTokenSource? _cancellation;
    private RunPosition _position;
    private bool _canResume;

    public RunStatusViewModel(
        SequenceRunner runner,
        IProfileStore store,
        Profile profile,
        Sequence sequence,
        IReadOnlyDictionary<Guid, ScreenAssets> screens,
        PixelRect targetRegion)
    {
        _runner = runner;
        _store = store;
        _profile = profile;
        _sequence = sequence;
        _screens = screens;
        _targetRegion = targetRegion;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => OnPropertyChanged(nameof(ElapsedText));
        StepText = $"{sequence.Steps.Count} step(s)";
    }

    /// <summary>Raised when the status window must close.</summary>
    public event EventHandler? CloseRequested;

    public string SequenceName => _sequence.Name;

    public string ElapsedText => _elapsed.Elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning), nameof(StateText))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand), nameof(ResumeCommand), nameof(CloseCommand))]
    public partial RunState State { get; set; } = RunState.Running;

    public bool IsRunning => State == RunState.Running;

    public string StateText => State switch
    {
        RunState.Running => "Running — press F12 to stop",
        RunState.Paused => "Paused",
        RunState.Stopped => "Stopped",
        RunState.Completed => "Completed",
        RunState.Failed => "Failed",
        _ => State.ToString(),
    };

    [ObservableProperty]
    public partial string IterationText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StepText { get; set; }

    [ObservableProperty]
    public partial string? ScoreText { get; set; }

    /// <summary>Explanation of the last outcome (pause reason, failure…).</summary>
    [ObservableProperty]
    public partial string? Message { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenLogImageCommand))]
    public partial string? LogImagePath { get; set; }

    /// <summary>Starts the run from the first step.</summary>
    public Task StartAsync() => RunFromAsync(default);

    private bool CanStop() => IsRunning;

    [RelayCommand(CanExecute = nameof(CanStop))]
    public void Stop() => _cancellation?.Cancel();

    private bool CanResume() => !IsRunning && _canResume;

    /// <summary>Resumes from the step that was interrupted (or failed).</summary>
    [RelayCommand(CanExecute = nameof(CanResume))]
    private Task ResumeAsync() => RunFromAsync(_position);

    private bool CanClose() => !IsRunning;

    [RelayCommand(CanExecute = nameof(CanClose))]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private bool CanOpenLogImage() => LogImagePath is not null;

    [RelayCommand(CanExecute = nameof(CanOpenLogImage))]
    private void OpenLogImage()
    {
        if (LogImagePath is not null)
        {
            Process.Start(new ProcessStartInfo(LogImagePath) { UseShellExecute = true });
        }
    }

    private async Task RunFromAsync(RunPosition start)
    {
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        Message = null;
        LogImagePath = null;
        ScoreText = null;
        State = RunState.Running;
        _elapsed.Start();
        _timer.Start();

        RunResult result;
        try
        {
            var progress = new Progress<RunProgress>(OnProgress);
            result = await _runner.RunAsync(_profile, _screens, _sequence, _targetRegion, start, progress, cancellation.Token);
        }
        catch (Exception ex)
        {
            // An unexpected error (e.g. capture or input failure) must not leave the window stuck in "Running".
            result = new RunResult(RunStatus.InvalidStep, _position) { Message = $"Unexpected error: {ex.Message}" };
        }
        finally
        {
            _elapsed.Stop();
            _timer.Stop();
            _cancellation = null;
            OnPropertyChanged(nameof(ElapsedText));
        }

        HandleResult(result);
    }

    private void OnProgress(RunProgress progress)
    {
        _position = progress.Position;
        IterationText = _sequence.RepeatCount == 0
            ? $"Iteration {progress.Position.Iteration + 1}"
            : $"Iteration {progress.Position.Iteration + 1} / {_sequence.RepeatCount}";
        StepText = $"Step {progress.Position.StepIndex + 1} / {progress.StepCount}: {DescribeStep(progress.Position.StepIndex)}";
        ScoreText = progress.MatchScore is { } score ? $"Match score: {score:0.00}" : "Not verified";
    }

    private void HandleResult(RunResult result)
    {
        _position = result.Position;
        _canResume = result.Status is RunStatus.Cancelled or RunStatus.UserTookOver or RunStatus.VerificationFailed;
        Message = result.Message;

        switch (result.Status)
        {
            case RunStatus.Completed:
                State = RunState.Completed;
                Message = "All steps were executed.";
                break;
            case RunStatus.Cancelled:
                State = RunState.Stopped;
                Message = $"Stopped before step {result.Position.StepIndex + 1}. Resume continues from there.";
                break;
            case RunStatus.UserTookOver:
                State = RunState.Paused;
                Message = $"The mouse was moved. Resume continues from step {result.Position.StepIndex + 1}.";
                break;
            case RunStatus.VerificationFailed:
                State = RunState.Failed;
                SaveFailureImage(result);
                break;
            default:
                State = RunState.Failed;
                break;
        }

        ResumeCommand.NotifyCanExecuteChanged();
    }

    private void SaveFailureImage(RunResult result)
    {
        if (result.LastCapture is not { } capture)
        {
            return;
        }

        var image = result.ExpectedBounds is { } expected ? capture.WithRectangle(expected, 255, 0, 0) : capture;
        try
        {
            LogImagePath = _store.SaveLogImage(_profile.Id, $"{_sequence.Name}-step{result.Position.StepIndex + 1}", image);
        }
        catch (PersistenceException ex)
        {
            Message += $" (The capture could not be saved: {ex.Message})";
        }
    }

    private string DescribeStep(int stepIndex)
    {
        var step = _sequence.Steps[stepIndex];
        return _screens.TryGetValue(step.ScreenId, out var assets) && assets.Screen.FindLocation(step.LocationId) is { } location
            ? $"{assets.Screen.Name} / {location.Name}"
            : "?";
    }
}
