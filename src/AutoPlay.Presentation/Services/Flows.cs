using AutoPlay.Domain.Model;

namespace AutoPlay.Presentation.Services;

/// <summary>Records a new screen (framing, capture, annotation) or edits an existing one.</summary>
public interface IScreenRecordingFlow
{
    /// <summary>Returns true if a screen was saved.</summary>
    Task<bool> RecordNewAsync(Profile profile);

    /// <summary>Returns true if the screen was saved.</summary>
    bool Edit(Profile profile, Screen screen);

    /// <summary>
    /// Captures the screen again (framing first), re-positions its locations on the new capture and opens the editor.
    /// Returns true if the screen was saved.
    /// </summary>
    Task<bool> RecaptureAsync(Profile profile, Screen screen);
}

/// <summary>Opens the settings window.</summary>
public interface ISettingsFlow
{
    /// <summary>Returns true if the settings were saved.</summary>
    /// <param name="profile">The selected profile, whose defaults can be edited; null if none.</param>
    bool Open(Profile? profile);
}

/// <summary>Opens the sequence editor.</summary>
public interface ISequenceEditingFlow
{
    /// <summary>Creates (when <paramref name="sequence"/> is null) or edits a sequence. Returns true if it was saved.</summary>
    bool Edit(Profile profile, Sequence? sequence);
}

/// <summary>How a sequence is started.</summary>
/// <param name="StartStep">Index of the step of the sequence to start from (0 = the beginning).</param>
/// <param name="Unattended">
/// True for a scheduled start: no framing confirmation, the profile's last target region is used.
/// </param>
public sealed record RunOptions(int StartStep = 0, bool Unattended = false);

/// <summary>Runs a sequence: framing of the target region, then the run status window.</summary>
public interface ISequenceExecutionFlow
{
    /// <summary>Whether a run is in progress (its status window is open).</summary>
    bool IsRunning { get; }

    /// <summary>Runs the sequence; returns false if it could not start (invalid sequence, framing cancelled…).</summary>
    Task<bool> RunAsync(Profile profile, Sequence sequence, RunOptions? options = null);
}
