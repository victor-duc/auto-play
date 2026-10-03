using AutoPlay.Core.Geometry;

namespace AutoPlay.Core.Recording;

/// <summary>A location being edited, in pixel coordinates of the screen capture.</summary>
public sealed record LocationDraft
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; init; }

    public PixelRect Bounds { get; init; }

    public PixelPoint ClickPoint { get; init; }

    public double? MatchThreshold { get; init; }
}
