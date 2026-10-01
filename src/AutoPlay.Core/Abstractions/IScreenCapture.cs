using AutoPlay.Core.Geometry;
using AutoPlay.Core.Imaging;

namespace AutoPlay.Core.Abstractions;

public interface IScreenCapture
{
    /// <summary>Captures an area of the physical screen (virtual desktop coordinates, physical pixels).</summary>
    RawImage Capture(PixelRect area);
}
