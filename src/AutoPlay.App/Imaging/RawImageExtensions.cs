using System.Windows.Media;
using System.Windows.Media.Imaging;
using AutoPlay.Domain.Imaging;

namespace AutoPlay.App.Imaging;

internal static class RawImageExtensions
{
    public static BitmapSource ToBitmapSource(this RawImage image)
    {
        var bitmap = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null, image.Pixels, image.Stride);
        bitmap.Freeze();
        return bitmap;
    }
}
