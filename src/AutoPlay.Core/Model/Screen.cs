using AutoPlay.Core.Geometry;

namespace AutoPlay.Core.Model;

/// <summary>A named state of the target application and the locations it contains.</summary>
public sealed class Screen
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    /// <summary>Size of the target region when the screen was recorded; used to scale template images.</summary>
    public PixelSize RecordedRegionSize { get; set; }

    public DelayRange DefaultDelay { get; set; } = new(800, 1500);

    public List<Location> Locations { get; set; } = [];

    public Location? FindLocation(Guid locationId) => Locations.Find(l => l.Id == locationId);
}
