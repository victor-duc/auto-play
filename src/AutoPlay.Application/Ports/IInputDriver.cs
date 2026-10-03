using AutoPlay.Domain.Geometry;

namespace AutoPlay.Application.Ports;

public interface IInputDriver
{
    /// <summary>Moves the cursor to <paramref name="point"/> and performs a left click.</summary>
    void Click(PixelPoint point);

    PixelPoint GetCursorPosition();
}
