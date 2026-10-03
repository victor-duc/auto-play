using AutoPlay.Domain.Imaging;
using OpenCvSharp;

namespace AutoPlay.Adapters.Vision;

internal static class MatConversions
{
    /// <summary>Copies a BGRA <see cref="RawImage"/> into a new 4-channel <see cref="Mat"/>.</summary>
    public static Mat ToMat(this RawImage image)
    {
        var mat = new Mat(image.Height, image.Width, MatType.CV_8UC4);
        System.Runtime.InteropServices.Marshal.Copy(image.Pixels, 0, mat.Data, image.Pixels.Length);
        return mat;
    }
}
