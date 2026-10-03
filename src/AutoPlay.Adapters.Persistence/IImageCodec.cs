using AutoPlay.Domain.Imaging;

namespace AutoPlay.Adapters.Persistence;

/// <summary>Encodes and decodes images to and from PNG.</summary>
public interface IImageCodec
{
    byte[] EncodePng(RawImage image);

    RawImage Decode(byte[] data);
}
