using AutoPlay.Domain.Model;
using AutoPlay.Presentation.Services;

namespace AutoPlay.Presentation.Tests;

internal sealed class FakeDialogs : IUserDialogs
{
    public bool ConfirmAnswer { get; set; } = true;

    public List<string> Confirmations { get; } = [];

    public List<string> Errors { get; } = [];

    public bool Confirm(string message, string title)
    {
        Confirmations.Add(message);
        return ConfirmAnswer;
    }

    public void ShowError(string message) => Errors.Add(message);
}

internal sealed class FakeShell : IShellLauncher
{
    public List<string> Opened { get; } = [];

    public void Open(string path) => Opened.Add(path);
}

/// <summary>Flows that do not open windows; the result is scripted.</summary>
internal sealed class FakeFlows : IScreenRecordingFlow, ISequenceEditingFlow, ISequenceExecutionFlow
{
    public Func<Profile, bool> OnRecord { get; set; } = _ => false;

    public Func<Profile, Sequence?, bool> OnEditSequence { get; set; } = (_, _) => false;

    public List<Sequence> Runs { get; } = [];

    public Task<bool> RecordNewAsync(Profile profile) => Task.FromResult(OnRecord(profile));

    public bool Edit(Profile profile, Screen screen) => false;

    public bool Edit(Profile profile, Sequence? sequence) => OnEditSequence(profile, sequence);

    public List<RunOptions> RunOptions { get; } = [];

    public bool IsRunning { get; set; }

    public bool RunResult { get; set; } = true;

    public Task<bool> RunAsync(Profile profile, Sequence sequence, RunOptions? options = null)
    {
        Runs.Add(sequence);
        RunOptions.Add(options ?? new RunOptions());
        return Task.FromResult(RunResult);
    }
}
