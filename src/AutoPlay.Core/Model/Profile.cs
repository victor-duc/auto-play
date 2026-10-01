using AutoPlay.Core.Geometry;

namespace AutoPlay.Core.Model;

/// <summary>A group of screens and sequences belonging to one target application.</summary>
public sealed class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    /// <summary>The last target region used for recording or execution, used to pre-position the overlay.</summary>
    public PixelRect? LastTargetRegion { get; set; }

    public ProfileDefaults Defaults { get; set; } = new();
}
