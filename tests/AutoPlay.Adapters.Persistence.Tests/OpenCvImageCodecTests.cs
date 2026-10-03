namespace AutoPlay.Adapters.Persistence.Tests;

public class OpenCvImageCodecTests
{
    [Fact]
    public void Png_round_trip_preserves_pixels()
    {
        var codec = new OpenCvImageCodec();
        var image = TestPatterns.Noise(37, 23, seed: 9);

        var png = codec.EncodePng(image);
        var decoded = codec.Decode(png);

        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], png[..4]);
        Assert.Equal(image.Size, decoded.Size);
        Assert.Equal(image.Pixels, decoded.Pixels);
    }

    [Fact]
    public void Decoding_invalid_data_throws()
    {
        Assert.ThrowsAny<Exception>(() => new OpenCvImageCodec().Decode([1, 2, 3, 4]));
    }
}
