namespace AutoPlay.Core.Geometry;

/// <summary>A size in physical screen pixels.</summary>
public readonly record struct PixelSize(int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}
