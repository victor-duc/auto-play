using AutoPlay.Core.Imaging;

namespace AutoPlay.Core.Abstractions;

/// <summary>Encodes and decodes images to and from PNG.</summary>
public interface IImageCodec
{
    byte[] EncodePng(RawImage image);

    RawImage Decode(byte[] data);
}
