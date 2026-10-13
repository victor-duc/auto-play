using AutoPlay.Domain.Model;

namespace AutoPlay.Presentation.Services;

/// <summary>Records a new screen (framing, capture, annotation) or edits an existing one.</summary>
public interface IScreenRecordingFlow
{
    /// <summary>Returns true if a screen was saved.</summary>
    Task<bool> RecordNewAsync(Profile profile);

    /// <summary>Returns true if the screen was saved.</summary>
    bool Edit(Profile profile, Screen screen);
}

/// <summary>Opens the sequence editor.</summary>
public interface ISequenceEditingFlow
{
    /// <summary>Creates (when <paramref name="sequence"/> is null) or edits a sequence. Returns true if it was saved.</summary>
    bool Edit(Profile profile, Sequence? sequence);
}

/// <summary>Runs a sequence: framing of the target region, then the run status window.</summary>
public interface ISequenceExecutionFlow
{
    Task RunAsync(Profile profile, Sequence sequence);
}
