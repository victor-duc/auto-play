namespace AutoPlay.Domain.Geometry;

/// <summary>A rectangle expressed as fractions (0.0–1.0) of a target region.</summary>
public readonly record struct NormalizedRect(double X, double Y, double Width, double Height)
{
    public bool Contains(NormalizedPoint point) =>
        point.X >= X && point.X <= X + Width && point.Y >= Y && point.Y <= Y + Height;

    public NormalizedPoint Center => new(X + Width / 2, Y + Height / 2);
}
