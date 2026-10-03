using AutoPlay.Core.Abstractions;
using AutoPlay.Domain.Imaging;
using OpenCvSharp;

namespace AutoPlay.Vision;

public sealed class OpenCvImageCodec : IImageCodec
{
    public byte[] EncodePng(RawImage image)
    {
        using var mat = image.ToMat();
        return mat.ToBytes(".png");
    }

    public RawImage Decode(byte[] data)
    {
        using var mat = Cv2.ImDecode(data, ImreadModes.Unchanged);
        if (mat.Empty())
        {
            throw new InvalidDataException("The data is not a supported image.");
        }

        return mat.ToRawImage();
    }
}
