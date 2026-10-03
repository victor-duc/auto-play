namespace AutoPlay.Domain.Geometry;

/// <summary>A point in physical screen pixels.</summary>
public readonly record struct PixelPoint(int X, int Y)
{
    public PixelPoint Offset(int dx, int dy) => new(X + dx, Y + dy);

    public double DistanceTo(PixelPoint other)
    {
        var dx = (double)(other.X - X);
        var dy = (double)(other.Y - Y);
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
