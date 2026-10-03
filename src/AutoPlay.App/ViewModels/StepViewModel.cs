using System.Globalization;
using System.Windows.Media.Imaging;
using AutoPlay.Domain.Model;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoPlay.App.ViewModels;

/// <summary>A sequence step being edited. Override fields are kept as text until the sequence is saved.</summary>
public sealed partial class StepViewModel : ObservableObject
{
    private readonly ProfileDefaults _defaults;

    /// <param name="target">The location of the step, or null if it no longer exists.</param>
    public StepViewModel(SequenceStep step, LocationChoice? target, ProfileDefaults defaults)
    {
        _defaults = defaults;
        ScreenId = step.ScreenId;
        LocationId = step.LocationId;
        Target = target;

        var delay = step.Delay ?? ScreenDefaultDelay;
        Verify = step.Verify;
        OverrideDelay = step.Delay is not null;
        DelayMinMs = delay.MinMs.ToString(CultureInfo.CurrentCulture);
        DelayMaxMs = delay.MaxMs.ToString(CultureInfo.CurrentCulture);
        OverrideTimeout = step.VerificationTimeoutMs is not null;
        TimeoutMs = (step.VerificationTimeoutMs ?? defaults.VerificationTimeoutMs).ToString(CultureInfo.CurrentCulture);
    }

    public Guid ScreenId { get; }

    public Guid LocationId { get; }

    public LocationChoice? Target { get; }

    public bool IsMissing => Target is null;

    public BitmapSource? Thumbnail => Target?.Thumbnail;

    public string Title => Target?.FullName ?? "Missing screen or location";

    [ObservableProperty]
    public partial int Number { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial bool Verify { get; set; }

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
    public string Summary
    {
        get
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
    }

    private DelayRange ScreenDefaultDelay => Target?.Screen.DefaultDelay ?? new Screen { Name = string.Empty }.DefaultDelay;

    public StepViewModel Clone()
    {
        var clone = new StepViewModel(new SequenceStep { ScreenId = ScreenId, LocationId = LocationId }, Target, _defaults)
        {
            Verify = Verify,
            OverrideDelay = OverrideDelay,
            DelayMinMs = DelayMinMs,
            DelayMaxMs = DelayMaxMs,
            OverrideTimeout = OverrideTimeout,
            TimeoutMs = TimeoutMs,
        };
        return clone;
    }

    /// <summary>Converts to a model step; returns an error message if an override cannot be parsed.</summary>
    public string? TryToStep(out SequenceStep step)
    {
        step = null!;
        DelayRange? delay = null;
        if (OverrideDelay)
        {
            if (!TryParseMs(DelayMinMs, out var min) || !TryParseMs(DelayMaxMs, out var max) || max < min)
            {
                return $"The delay of step {Number} must be two positive whole numbers of milliseconds, the maximum not lower than the minimum.";
            }

            delay = new DelayRange(min, max);
        }

        int? timeout = null;
        if (Verify && OverrideTimeout)
        {
            if (!TryParseMs(TimeoutMs, out var value))
            {
                return $"The verification timeout of step {Number} must be a positive whole number of milliseconds.";
            }

            timeout = value;
        }

        step = new SequenceStep
        {
            ScreenId = ScreenId,
            LocationId = LocationId,
            Delay = delay,
            VerificationTimeoutMs = timeout,
            Verify = Verify,
        };
        return null;
    }

    private static bool TryParseMs(string text, out int value) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out value) && value >= 0;
}
