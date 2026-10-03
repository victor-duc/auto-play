using AutoPlay.Core.Geometry;

namespace AutoPlay.Core.Abstractions;

public interface IInputDriver
{
    /// <summary>Moves the cursor to <paramref name="point"/> and performs a left click.</summary>
    void Click(PixelPoint point);

    PixelPoint GetCursorPosition();
}
