using AutoPlay.Domain.Imaging;
using OpenCvSharp;

namespace AutoPlay.Adapters.Persistence;

internal static class MatConversions
{
    /// <summary>Copies a BGRA <see cref="RawImage"/> into a new 4-channel <see cref="Mat"/>.</summary>
    public static Mat ToMat(this RawImage image)
    {
        var mat = new Mat(image.Height, image.Width, MatType.CV_8UC4);
        System.Runtime.InteropServices.Marshal.Copy(image.Pixels, 0, mat.Data, image.Pixels.Length);
        return mat;
    }

    /// <summary>Converts a 1, 3 or 4-channel 8-bit <see cref="Mat"/> into a BGRA <see cref="RawImage"/>.</summary>
    public static RawImage ToRawImage(this Mat mat)
    {
        using var bgra = new Mat();
        switch (mat.Channels())
        {
            case 1:
                Cv2.CvtColor(mat, bgra, ColorConversionCodes.GRAY2BGRA);
                break;
            case 3:
                Cv2.CvtColor(mat, bgra, ColorConversionCodes.BGR2BGRA);
                break;
            case 4:
                mat.CopyTo(bgra);
                break;
            default:
                throw new NotSupportedException($"Images with {mat.Channels()} channels are not supported.");
        }

        var pixels = new byte[bgra.Width * bgra.Height * RawImage.BytesPerPixel];
        if (!bgra.GetArray(out Vec4b[] data))
        {
            throw new InvalidOperationException("Unable to read the image pixels.");
        }

        System.Runtime.InteropServices.MemoryMarshal.AsBytes(data.AsSpan()).CopyTo(pixels);
        return new RawImage(bgra.Width, bgra.Height, pixels);
    }
}
