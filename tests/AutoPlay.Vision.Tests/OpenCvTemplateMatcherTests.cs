using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Imaging;

namespace AutoPlay.Vision.Tests;

public class OpenCvTemplateMatcherTests
{
    private readonly OpenCvTemplateMatcher _matcher = new();

    [Fact]
    public void Finds_the_template_at_its_position()
    {
        var image = TestPatterns.Noise(200, 120, seed: 1);
        var template = image.Crop(new PixelRect(70, 40, 30, 20));

        var match = _matcher.FindBestMatch(image, template, template.Size);

        Assert.Equal(new PixelRect(70, 40, 30, 20), match.Bounds);
        Assert.True(match.Score > 0.99, $"Score was {match.Score}.");
    }

    [Fact]
    public void Finds_a_scaled_template()
    {
        // The template was recorded at twice the current size.
        var image = TestPatterns.Blocks(200, 120, blockSize: 10, seed: 2);
        var recorded = TestPatterns.Upscale(image.Crop(new PixelRect(60, 40, 40, 30)), 2);

        var match = _matcher.FindBestMatch(image, recorded, new PixelSize(40, 30));

        Assert.Equal(new PixelRect(60, 40, 40, 30), match.Bounds);
        Assert.True(match.Score > 0.9, $"Score was {match.Score}.");
    }

    [Fact]
    public void Tolerates_a_partially_covered_element()
    {
        var image = TestPatterns.Blocks(200, 120, blockSize: 10, seed: 3);
        var template = image.Crop(new PixelRect(50, 30, 60, 40));
        TestPatterns.FillRect(image, new PixelRect(50, 30, 12, 10), 0xFF); // A "sparkle" over a corner.

        var match = _matcher.FindBestMatch(image, template, template.Size);

        Assert.Equal(new PixelRect(50, 30, 60, 40), match.Bounds);
        Assert.True(match.Score > 0.8, $"Score was {match.Score}.");
    }

    [Fact]
    public void Scores_low_when_the_element_is_absent()
    {
        var image = TestPatterns.Noise(200, 120, seed: 4);
        var template = TestPatterns.Noise(30, 20, seed: 5);

        var match = _matcher.FindBestMatch(image, template, template.Size);

        Assert.True(match.Score < 0.5, $"Score was {match.Score}.");
    }

    [Fact]
    public void Template_larger_than_the_image_scores_zero()
    {
        var match = _matcher.FindBestMatch(TestPatterns.Noise(20, 20, 6), TestPatterns.Noise(30, 30, 7), new PixelSize(30, 30));

        Assert.Equal(0, match.Score);
    }

    [Fact]
    public void Uniform_template_does_not_produce_an_invalid_score()
    {
        var uniform = new RawImage(10, 10, Enumerable.Repeat((byte)0x80, 400).ToArray());

        var match = _matcher.FindBestMatch(TestPatterns.Noise(50, 50, 8), uniform, uniform.Size);

        Assert.InRange(match.Score, 0, 1);
    }
}
