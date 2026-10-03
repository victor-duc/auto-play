using AutoPlay.Domain.Geometry;

namespace AutoPlay.Domain.Model;

/// <summary>A named clickable element of a screen. The content of <see cref="Bounds"/> is its template image.</summary>
public sealed class Location
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    public PositioningMode Positioning { get; set; } = PositioningMode.Proportional;

    /// <summary>The element rectangle, relative to the target region.</summary>
    public NormalizedRect Bounds { get; set; }

    /// <summary>The click point, relative to the target region. Must lie within <see cref="Bounds"/>.</summary>
    public NormalizedPoint ClickPoint { get; set; }

    /// <summary>Overrides the profile's match threshold when set.</summary>
    public double? MatchThreshold { get; set; }
}
