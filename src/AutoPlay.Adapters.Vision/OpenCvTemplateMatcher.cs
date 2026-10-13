using AutoPlay.Application.Ports;
using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Imaging;
using OpenCvSharp;

namespace AutoPlay.Adapters.Vision;

/// <summary>
/// Template matching with OpenCV. The position is found with the normalized correlation coefficient on
/// grayscale images, which is robust to lighting and color effects. When colors must match, the score is
/// also limited by the color similarity of the matched area, based on the chroma difference in the Lab space.
/// </summary>
public sealed class OpenCvTemplateMatcher : ITemplateMatcher
{
    public MatchResult FindBestMatch(RawImage image, RawImage templateImage, PixelSize templateSize, bool matchColors)
    {
        if (templateSize.IsEmpty || templateSize.Width > image.Width || templateSize.Height > image.Height)
        {
            return new MatchResult(default, 0);
        }

        using var haystack = ToBgr(image);
        using var needle = ToBgr(templateImage);
        if (needle.Width != templateSize.Width || needle.Height != templateSize.Height)
        {
            var interpolation = templateSize.Width < needle.Width ? InterpolationFlags.Area : InterpolationFlags.Linear;
            Cv2.Resize(needle, needle, new Size(templateSize.Width, templateSize.Height), interpolation: interpolation);
        }

        using var grayHaystack = new Mat();
        using var grayNeedle = new Mat();
        Cv2.CvtColor(haystack, grayHaystack, ColorConversionCodes.BGR2GRAY);
        Cv2.CvtColor(needle, grayNeedle, ColorConversionCodes.BGR2GRAY);

        using var scores = new Mat();
        Cv2.MatchTemplate(grayHaystack, grayNeedle, scores, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(scores, out _, out var maxScore, out _, out var maxLocation);

        // A uniform template has no variance, which makes the normalized score undefined.
        var score = double.IsFinite(maxScore) ? Math.Clamp(maxScore, 0, 1) : 0;
        var bounds = new PixelRect(maxLocation.X, maxLocation.Y, templateSize.Width, templateSize.Height);

        if (matchColors)
        {
            using var matched = new Mat(haystack, new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height));
            score = Math.Min(score, ColorSimilarity(needle, matched));
        }

        return new MatchResult(bounds, score);
    }

    /// <summary>
    /// Mean chroma difference (Lab a and b channels, in 8-bit units) at which colors are considered unrelated.
    /// Brightness is ignored here: the grayscale correlation already covers it.
    /// </summary>
    private const double UnrelatedChromaDifference = 24;

    /// <summary>1 when both images have the same colors, 0 when their colors are unrelated.</summary>
    private static double ColorSimilarity(Mat expected, Mat actual)
    {
        using var expectedLab = new Mat();
        using var actualLab = new Mat();
        Cv2.CvtColor(expected, expectedLab, ColorConversionCodes.BGR2Lab);
        Cv2.CvtColor(actual, actualLab, ColorConversionCodes.BGR2Lab);
        using var difference = new Mat();
        Cv2.Absdiff(expectedLab, actualLab, difference);
        var mean = Cv2.Mean(difference);
        var chromaDifference = (mean.Val1 + mean.Val2) / 2;
        return Math.Max(0, 1 - chromaDifference / UnrelatedChromaDifference);
    }

    private static Mat ToBgr(RawImage image)
    {
        using var bgra = image.ToMat();
        var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        return bgr;
    }
}
