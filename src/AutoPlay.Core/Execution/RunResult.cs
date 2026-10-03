using AutoPlay.Core.Geometry;
using AutoPlay.Core.Imaging;

namespace AutoPlay.Core.Execution;

public enum RunStatus
{
    /// <summary>All iterations were executed.</summary>
    Completed,

    /// <summary>The run was cancelled (e.g. emergency stop).</summary>
    Cancelled,

    /// <summary>The user moved the mouse; the run can be resumed from <see cref="RunResult.Position"/>.</summary>
    UserTookOver,

    /// <summary>A location was not found before the verification timeout.</summary>
    VerificationFailed,

    /// <summary>A step references a screen or location that does not exist.</summary>
    InvalidStep,
}

/// <summary>The outcome of a sequence run.</summary>
public sealed record RunResult(RunStatus Status, RunPosition Position)
{
    /// <summary>Human-readable explanation for statuses other than <see cref="RunStatus.Completed"/>.</summary>
    public string? Message { get; init; }

    /// <summary>For <see cref="RunStatus.VerificationFailed"/>: the best score obtained.</summary>
    public double? BestScore { get; init; }

    /// <summary>For <see cref="RunStatus.VerificationFailed"/>: the last capture of the search area.</summary>
    public RawImage? LastCapture { get; init; }

    /// <summary>For <see cref="RunStatus.VerificationFailed"/>: where the location was expected, relative to <see cref="LastCapture"/>.</summary>
    public PixelRect? ExpectedBounds { get; init; }
}
