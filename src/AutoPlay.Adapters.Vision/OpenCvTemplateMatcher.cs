using AutoPlay.Application.Ports;
using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Imaging;
using OpenCvSharp;

namespace AutoPlay.Adapters.Vision;

/// <summary>
/// Template matching with OpenCV's normalized correlation coefficient, on grayscale images
/// to reduce sensitivity to color effects.
/// </summary>
public sealed class OpenCvTemplateMatcher : ITemplateMatcher
{
    public MatchResult FindBestMatch(RawImage image, RawImage templateImage, PixelSize templateSize)
    {
        if (templateSize.IsEmpty || templateSize.Width > image.Width || templateSize.Height > image.Height)
        {
            return new MatchResult(default, 0);
        }

        using var haystack = ToGray(image);
        using var needle = ToGray(templateImage);
        if (needle.Width != templateSize.Width || needle.Height != templateSize.Height)
        {
            var interpolation = templateSize.Width < needle.Width ? InterpolationFlags.Area : InterpolationFlags.Linear;
            Cv2.Resize(needle, needle, new Size(templateSize.Width, templateSize.Height), interpolation: interpolation);
        }

        using var scores = new Mat();
        Cv2.MatchTemplate(haystack, needle, scores, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(scores, out _, out var maxScore, out _, out var maxLocation);

        // A uniform template has no variance, which makes the normalized score undefined.
        var score = double.IsFinite(maxScore) ? Math.Clamp(maxScore, 0, 1) : 0;
        return new MatchResult(new PixelRect(maxLocation.X, maxLocation.Y, templateSize.Width, templateSize.Height), score);
    }

    private static Mat ToGray(RawImage image)
    {
        using var bgra = image.ToMat();
        var gray = new Mat();
        Cv2.CvtColor(bgra, gray, ColorConversionCodes.BGRA2GRAY);
        return gray;
    }
}
