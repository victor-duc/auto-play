using System.Globalization;
using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Model;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoPlay.Presentation.ViewModels;

/// <summary>A sequence step being edited. Durations and overrides are kept as text until the sequence is saved.</summary>
public sealed partial class StepViewModel : ObservableObject
{
    /// <summary>Default duration of a new wait step, in milliseconds.</summary>
    public const int DefaultWaitMs = 1000;

    private readonly ProfileDefaults _defaults;

    /// <param name="target">For a click: the location, or null if it no longer exists.</param>
    /// <param name="subSequence">For a run of another sequence: that sequence, or null if it no longer exists.</param>
    public StepViewModel(SequenceStep step, LocationChoice? target, SequenceChoice? subSequence, ProfileDefaults defaults)
    {
        _defaults = defaults;
        Kind = step.Kind;
        ScreenId = step.ScreenId;
        LocationId = step.LocationId;
        Target = target;
        SequenceId = step.SequenceId;
        SubSequence = subSequence;

        var delay = step.Delay ?? (Kind == StepKind.Wait ? new DelayRange(DefaultWaitMs, DefaultWaitMs) : ScreenDefaultDelay);
        Verify = step.Verify;
        OverrideDelay = step.Delay is not null || Kind == StepKind.Wait;
        DelayMinMs = delay.MinMs.ToString(CultureInfo.CurrentCulture);
        DelayMaxMs = delay.MaxMs.ToString(CultureInfo.CurrentCulture);
        OverrideTimeout = step.VerificationTimeoutMs is not null;
        TimeoutMs = (step.VerificationTimeoutMs ?? defaults.VerificationTimeoutMs).ToString(CultureInfo.CurrentCulture);
    }

    public StepKind Kind { get; }

    public bool IsClick => Kind == StepKind.Click;

    public bool IsWait => Kind == StepKind.Wait;

    public bool IsRunSequence => Kind == StepKind.RunSequence;

    public Guid ScreenId { get; }

    public Guid LocationId { get; }

    public LocationChoice? Target { get; }

    public Guid? SequenceId { get; }

    public SequenceChoice? SubSequence { get; }

    /// <summary>Whether the location or sequence the step refers to was deleted.</summary>
    public bool IsMissing => Kind switch
    {
        StepKind.Click => Target is null,
        StepKind.RunSequence => SubSequence is null,
        _ => false,
    };

    public RawImage? Thumbnail => Target?.Thumbnail;

    public string Title => Kind switch
    {
        StepKind.Wait => "Wait",
        StepKind.RunSequence => SubSequence is { } sequence ? $"Run '{sequence.Name}'" : "Missing sequence",
        _ => Target?.FullName ?? "Missing screen or location",
    };

    [ObservableProperty]
    public partial int Number { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial bool Verify { get; set; }

    /// <summary>For a click: whether the step has its own delay. Always true for a wait.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial bool OverrideDelay { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial string DelayMinMs { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial string DelayMaxMs { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial bool OverrideTimeout { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial string TimeoutMs { get; set; }

    /// <summary>One-line description of the effective settings of the step.</summary>
    public string Summary => Kind switch
    {
        StepKind.Wait => $"{DelayMinMs}–{DelayMaxMs} ms",
        StepKind.RunSequence => "Runs all its steps, as many times as its repeat count",
        _ => ClickSummary(),
    };

    private DelayRange ScreenDefaultDelay => Target?.Screen.DefaultDelay ?? new Screen { Name = string.Empty }.DefaultDelay;

    public StepViewModel Clone() =>
        new(new SequenceStep { Kind = Kind, ScreenId = ScreenId, LocationId = LocationId, SequenceId = SequenceId }, Target, SubSequence, _defaults)
        {
            Verify = Verify,
            OverrideDelay = OverrideDelay,
            DelayMinMs = DelayMinMs,
            DelayMaxMs = DelayMaxMs,
            OverrideTimeout = OverrideTimeout,
            TimeoutMs = TimeoutMs,
        };

    /// <summary>Converts to a model step; returns an error message if a value cannot be parsed.</summary>
    public string? TryToStep(out SequenceStep step)
    {
        step = null!;
        DelayRange? delay = null;
        if (OverrideDelay || Kind == StepKind.Wait)
        {
            if (!TryParseMs(DelayMinMs, out var min) || !TryParseMs(DelayMaxMs, out var max) || max < min)
            {
                var what = Kind == StepKind.Wait ? "duration" : "delay";
                return $"The {what} of step {Number} must be two positive whole numbers of milliseconds, the maximum not lower than the minimum.";
            }

            delay = new DelayRange(min, max);
        }

        int? timeout = null;
        if (Kind == StepKind.Click && Verify && OverrideTimeout)
        {
            if (!TryParseMs(TimeoutMs, out var value))
            {
                return $"The verification timeout of step {Number} must be a positive whole number of milliseconds.";
            }

            timeout = value;
        }

        step = Kind switch
        {
            StepKind.Wait => SequenceStep.Wait(delay!),
            StepKind.RunSequence => new SequenceStep { Kind = StepKind.RunSequence, SequenceId = SequenceId },
            _ => new SequenceStep
            {
                ScreenId = ScreenId,
                LocationId = LocationId,
                Delay = delay,
                VerificationTimeoutMs = timeout,
                Verify = Verify,
            },
        };
        return null;
    }

    private string ClickSummary()
    {
        var delay = OverrideDelay
            ? $"Delay {DelayMinMs}–{DelayMaxMs} ms"
            : $"Delay {ScreenDefaultDelay.MinMs}–{ScreenDefaultDelay.MaxMs} ms (screen default)";
        var verification = !Verify
            ? "no verification"
            : OverrideTimeout
                ? $"verified, timeout {TimeoutMs} ms"
                : $"verified, timeout {_defaults.VerificationTimeoutMs} ms (default)";
        return $"{delay} · {verification}";
    }

    private static bool TryParseMs(string text, out int value) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out value) && value >= 0;
}
