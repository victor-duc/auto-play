using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Imaging;

namespace AutoPlay.Application.Ports;

public interface IScreenCapture
{
    /// <summary>Captures an area of the physical screen (virtual desktop coordinates, physical pixels).</summary>
    RawImage Capture(PixelRect area);
}
