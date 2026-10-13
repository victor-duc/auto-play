using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Tests.Fakes;

namespace AutoPlay.Domain.Tests.Imaging;

public class RawImageTests
{
    [Fact]
    public void Constructor_rejects_a_buffer_of_the_wrong_size()
    {
        Assert.Throws<ArgumentException>(() => new RawImage(2, 2, new byte[15]));
    }

    [Fact]
    public void Crop_copies_the_requested_area()
    {
        var image = TestImages.Gradient(10, 8);

        var crop = image.Crop(new PixelRect(3, 2, 4, 5));

        Assert.Equal(new PixelSize(4, 5), crop.Size);
        Assert.Equal(3, crop.Pixels[0]); // x of the top-left pixel
        Assert.Equal(2, crop.Pixels[1]); // y of the top-left pixel
        var last = crop.Pixels.Length - RawImage.BytesPerPixel;
        Assert.Equal(6, crop.Pixels[last]);
        Assert.Equal(6, crop.Pixels[last + 1]);
    }

    [Fact]
    public void Crop_rejects_an_area_outside_the_image()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TestImages.Gradient(10, 8).Crop(new PixelRect(8, 0, 4, 4)));
    }
}

public class RawImageDrawingTests
{
    [Fact]
    public void WithRectangle_draws_the_outline_only_and_keeps_the_original()
    {
        var image = AutoPlay.Domain.Tests.Fakes.TestImages.Solid(10, 10, 0);

        var drawn = image.WithRectangle(new AutoPlay.Domain.Geometry.PixelRect(2, 2, 6, 6), 255, 0, 0, thickness: 1);

        Assert.Equal(255, Red(drawn, 2, 2));
        Assert.Equal(255, Red(drawn, 7, 5));
        Assert.Equal(0, Red(drawn, 4, 4)); // Inside the outline.
        Assert.Equal(0, Red(drawn, 1, 1)); // Outside the rectangle.
        Assert.Equal(0, Red(image, 2, 2)); // The original is not modified.
    }

    [Fact]
    public void WithRectangle_is_clipped_to_the_image()
    {
        var image = AutoPlay.Domain.Tests.Fakes.TestImages.Solid(10, 10, 0);

        var drawn = image.WithRectangle(new AutoPlay.Domain.Geometry.PixelRect(-5, -5, 30, 30), 255, 0, 0);

        Assert.Equal(0, Red(drawn, 5, 5));
    }

    private static byte Red(AutoPlay.Domain.Imaging.RawImage image, int x, int y) =>
        image.Pixels[y * image.Stride + x * AutoPlay.Domain.Imaging.RawImage.BytesPerPixel + 2];

    [Fact]
    public void ScaleDown_keeps_the_proportions_and_averages_the_pixels()
    {
        // 4 x 2 image: left half black, right half white.
        var pixels = new byte[4 * 2 * 4];
        for (var y = 0; y < 2; y++)
        {
            for (var x = 2; x < 4; x++)
            {
                Array.Fill(pixels, (byte)255, ((y * 4) + x) * 4, 4);
            }
        }

        var scaled = new RawImage(4, 2, pixels).ScaleDown(2, 2);

        Assert.Equal(new PixelSize(2, 1), scaled.Size);
        Assert.Equal(0, scaled.Pixels[0]);
        Assert.Equal(255, scaled.Pixels[4]);
    }

    [Fact]
    public void ScaleDown_returns_the_image_when_it_already_fits()
    {
        var image = new RawImage(4, 2, new byte[4 * 2 * 4]);

        Assert.Same(image, image.ScaleDown(10, 10));
    }
}
