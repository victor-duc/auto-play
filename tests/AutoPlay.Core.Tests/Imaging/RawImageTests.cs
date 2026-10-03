using AutoPlay.Core.Geometry;
using AutoPlay.Core.Imaging;
using AutoPlay.Core.Tests.Fakes;

namespace AutoPlay.Core.Tests.Imaging;

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
        var image = AutoPlay.Core.Tests.Fakes.TestImages.Solid(10, 10, 0);

        var drawn = image.WithRectangle(new AutoPlay.Core.Geometry.PixelRect(2, 2, 6, 6), 255, 0, 0, thickness: 1);

        Assert.Equal(255, Red(drawn, 2, 2));
        Assert.Equal(255, Red(drawn, 7, 5));
        Assert.Equal(0, Red(drawn, 4, 4)); // Inside the outline.
        Assert.Equal(0, Red(drawn, 1, 1)); // Outside the rectangle.
        Assert.Equal(0, Red(image, 2, 2)); // The original is not modified.
    }

    [Fact]
    public void WithRectangle_is_clipped_to_the_image()
    {
        var image = AutoPlay.Core.Tests.Fakes.TestImages.Solid(10, 10, 0);

        var drawn = image.WithRectangle(new AutoPlay.Core.Geometry.PixelRect(-5, -5, 30, 30), 255, 0, 0);

        Assert.Equal(0, Red(drawn, 5, 5));
    }

    private static byte Red(AutoPlay.Core.Imaging.RawImage image, int x, int y) =>
        image.Pixels[y * image.Stride + x * AutoPlay.Core.Imaging.RawImage.BytesPerPixel + 2];
}
