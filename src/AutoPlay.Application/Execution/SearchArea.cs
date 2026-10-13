using AutoPlay.Domain.Geometry;

namespace AutoPlay.Application.Execution;

/// <summary>Where a location is looked for on the screen.</summary>
public static class SearchArea
{
    /// <summary>
    /// The expected bounds of a location, enlarged by <paramref name="margin"/> (a fraction of its size) on each side
    /// and limited to the target region.
    /// </summary>
    public static PixelRect Around(PixelRect expectedBounds, PixelRect targetRegion, double margin) =>
        expectedBounds
            .Inflate(
                CoordinateMapper.Round(expectedBounds.Width * margin),
                CoordinateMapper.Round(expectedBounds.Height * margin))
            .Intersect(targetRegion);
}
