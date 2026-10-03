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
