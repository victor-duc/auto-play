namespace AutoPlay.Application.UseCases;

/// <summary>The result of looking for a location on the live screen, without clicking.</summary>
/// <param name="OffsetX">Horizontal distance, in pixels, between the best match and the expected position.</param>
/// <param name="OffsetY">Vertical distance, in pixels, between the best match and the expected position.</param>
public sealed record LocationTestResult(string ScreenName, string LocationName, double Score, double Threshold, int OffsetX, int OffsetY)
{
    /// <summary>Whether a run would click this location.</summary>
    public bool Found => Score >= Threshold;
}
