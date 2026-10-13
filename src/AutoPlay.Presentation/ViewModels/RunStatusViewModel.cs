using System.Diagnostics;
using System.Globalization;
using AutoPlay.Application.Execution;
using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Model;
using AutoPlay.Application.Ports;
using AutoPlay.Application.UseCases;
using AutoPlay.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoPlay.Presentation.ViewModels;

public enum RunState
{
    Running,
    Paused,
    Stopped,
    Completed,
    Failed,
}

/// <summary>Runs a sequence and exposes its progress, with stop, resume and close actions.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "The timer only exists during a run and is disposed when the run ends.")]
public sealed partial class RunStatusViewModel : ObservableObject
{
    private readonly SequenceRunner _runner;
    private readonly SequenceExecutionService _executionService;
    private readonly Profile _profile;
    private readonly Sequence _sequence;
    private readonly IReadOnlyDictionary<Guid, ScreenAssets> _screens;
    private readonly PixelRect _targetRegion;
    private readonly Stopwatch _elapsed = new();
    private readonly IShellLauncher _shell;
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
    private Timer? _timer;
    private CancellationTokenSource? _cancellation;
    private RunPosition _position;
    private bool _canResume;

    public RunStatusViewModel(
        SequenceRunner runner,
        SequenceExecutionService executionService,
        Profile profile,
        Sequence sequence,
        IReadOnlyDictionary<Guid, ScreenAssets> screens,
        PixelRect targetRegion,
        IShellLauncher shell)
    {
        _runner = runner;
        _executionService = executionService;
        _profile = profile;
        _sequence = sequence;
        _screens = screens;
        _targetRegion = targetRegion;
        _shell = shell;
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
        RunState.Running => "Running — press Esc or F12 to stop",
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

    /// <summary>Where the first run starts (by default, the first step).</summary>
    public RunPosition StartPosition { get; init; }

    /// <summary>Starts the run from <see cref="StartPosition"/>.</summary>
    public Task StartAsync() => RunFromAsync(StartPosition);

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
            _shell.Open(LogImagePath);
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
        _timer = new Timer(_ => RaiseOnUiThread(nameof(ElapsedText)), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        RunResult result;
        try
        {
            // Created on the UI thread, so reports are marshalled back to it.
            var progress = new Progress<RunProgress>(OnProgress);

            // The engine runs on a worker thread: captures, matching and clicks never block the UI.
            result = await Task.Run(
                () => _runner.RunAsync(_profile, _screens, _sequence, _targetRegion, start, progress, cancellation.Token),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            // An unexpected error (e.g. capture or input failure) must not leave the window stuck in "Running".
            result = new RunResult(RunStatus.InvalidStep, _position) { Message = $"Unexpected error: {ex.Message}" };
        }
        finally
        {
            _elapsed.Stop();
            await _timer.DisposeAsync();
            _timer = null;
            _cancellation = null;
            OnPropertyChanged(nameof(ElapsedText));
        }

        HandleResult(result);
    }

    private void RaiseOnUiThread(string propertyName)
    {
        if (_uiContext is null)
        {
            OnPropertyChanged(propertyName);
        }
        else
        {
            _uiContext.Post(_ => OnPropertyChanged(propertyName), null);
        }
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
        _executionService.RecordRun(_profile.Id, _sequence.Id, result, DateTimeOffset.Now);
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
        try
        {
            LogImagePath = _executionService.SaveFailureCapture(_profile.Id, _sequence, result);
        }
        catch (PersistenceException ex)
        {
            Message += $" (The capture could not be saved: {ex.Message})";
        }
    }

    private string DescribeStep(int stepIndex)
    {
        var step = _sequence.Steps[stepIndex];
        if (step.Kind == StepKind.Wait)
        {
            return step.Delay is { } delay ? $"wait {delay.MinMs}–{delay.MaxMs} ms" : "wait";
        }

        return _screens.TryGetValue(step.ScreenId, out var assets) && assets.Screen.FindLocation(step.LocationId) is { } location
            ? $"{assets.Screen.Name} / {location.Name}"
            : "?";
    }
}
