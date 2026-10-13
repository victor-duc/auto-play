using AutoPlay.Domain.Geometry;

namespace AutoPlay.Domain.Recording;

/// <summary>A location being edited, in pixel coordinates of the screen capture.</summary>
public sealed record LocationDraft
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; init; }

    public PixelRect Bounds { get; init; }

    public PixelPoint ClickPoint { get; init; }

    public double? MatchThreshold { get; init; }

    public bool MatchColors { get; init; }

    /// <summary>Returns the draft moved to <paramref name="bounds"/>, the click point keeping its offset in the rectangle.</summary>
    public LocationDraft MovedTo(PixelRect bounds) => this with
    {
        Bounds = bounds,
        ClickPoint = RectEditing.Clamp(ClickPoint.Offset(bounds.Left - Bounds.Left, bounds.Top - Bounds.Top), bounds),
    };
}
