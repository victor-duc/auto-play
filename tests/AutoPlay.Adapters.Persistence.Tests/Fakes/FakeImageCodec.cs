using AutoPlay.Domain.Imaging;

namespace AutoPlay.Adapters.Persistence.Tests.Fakes;

/// <summary>A codec storing raw pixels with a small header, to test storage without OpenCV.</summary>
internal sealed class FakeImageCodec : IImageCodec
{
    public byte[] EncodePng(RawImage image) =>
        [.. BitConverter.GetBytes(image.Width), .. BitConverter.GetBytes(image.Height), .. image.Pixels];

    public RawImage Decode(byte[] data) =>
        new(BitConverter.ToInt32(data, 0), BitConverter.ToInt32(data, 4), data[8..]);
}
