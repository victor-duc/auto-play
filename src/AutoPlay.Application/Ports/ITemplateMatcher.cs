using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Imaging;

namespace AutoPlay.Application.Ports;

/// <summary>The best match of a template in an image.</summary>
/// <param name="Bounds">Position of the match, relative to the searched image.</param>
/// <param name="Score">Similarity score, from 0.0 (no similarity) to 1.0 (identical).</param>
public readonly record struct MatchResult(PixelRect Bounds, double Score);

public interface ITemplateMatcher
{
    /// <summary>
    /// Searches <paramref name="templateImage"/>, resized to <paramref name="templateSize"/>, in <paramref name="image"/>
    /// and returns the best match. Returns a score of 0 if the template does not fit in the image.
    /// </summary>
    MatchResult FindBestMatch(RawImage image, RawImage templateImage, PixelSize templateSize);
}
