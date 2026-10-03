using System.ComponentModel;
using System.Runtime.InteropServices;
using AutoPlay.Core.Abstractions;
using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Imaging;
using static AutoPlay.Windows.Interop.NativeMethods;

namespace AutoPlay.Windows.Capture;

/// <summary>
/// Captures the screen with GDI BitBlt. Coordinates are virtual-desktop physical pixels, which requires the
/// process to be per-monitor DPI aware (declared in the application manifest).
/// </summary>
public sealed class GdiScreenCapture : IScreenCapture
{
    public unsafe RawImage Capture(PixelRect area)
    {
        if (area.IsEmpty)
        {
            throw new ArgumentException("The capture area must not be empty.", nameof(area));
        }

        var screenDc = GetDC(0);
        var memoryDc = CreateCompatibleDC(screenDc);
        var bitmap = CreateCompatibleBitmap(screenDc, area.Width, area.Height);
        var previous = SelectObject(memoryDc, bitmap);
        try
        {
            if (!BitBlt(memoryDc, 0, 0, area.Width, area.Height, screenDc, area.Left, area.Top, SRCCOPY | CAPTUREBLT))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            var header = new BITMAPINFOHEADER
            {
                biSize = (uint)sizeof(BITMAPINFOHEADER),
                biWidth = area.Width,
                biHeight = -area.Height, // Negative height: top-down rows.
                biPlanes = 1,
                biBitCount = 32,
                biCompression = BI_RGB,
            };

            var pixels = new byte[area.Width * area.Height * RawImage.BytesPerPixel];
            int lines;
            fixed (byte* bits = pixels)
            {
                SelectObject(memoryDc, previous); // GetDIBits requires the bitmap not to be selected.
                previous = 0;
                lines = GetDIBits(memoryDc, bitmap, 0, (uint)area.Height, bits, ref header, DIB_RGB_COLORS);
            }

            if (lines != area.Height)
            {
                throw new InvalidOperationException("Unable to read the captured pixels.");
            }

            // GDI leaves the alpha channel undefined; make the image fully opaque.
            for (var i = 3; i < pixels.Length; i += RawImage.BytesPerPixel)
            {
                pixels[i] = 0xFF;
            }

            return new RawImage(area.Width, area.Height, pixels);
        }
        finally
        {
            if (previous != 0)
            {
                SelectObject(memoryDc, previous);
            }

            DeleteObject(bitmap);
            DeleteDC(memoryDc);
            _ = ReleaseDC(0, screenDc);
        }
    }
}
