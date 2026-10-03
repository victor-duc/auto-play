namespace AutoPlay.Domain.Geometry;

/// <summary>A point expressed as fractions (0.0–1.0) of a target region.</summary>
public readonly record struct NormalizedPoint(double X, double Y);
