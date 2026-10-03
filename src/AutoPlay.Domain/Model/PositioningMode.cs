namespace AutoPlay.Domain.Model;

/// <summary>How a location's position follows the target region when its size changes.</summary>
public enum PositioningMode
{
    /// <summary>The position scales with the target region (x% / y%).</summary>
    Proportional,

    /// <summary>Reserved: fixed pixel offset from an anchor corner. Not implemented in V1.</summary>
    Anchored,
}
