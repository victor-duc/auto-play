namespace AutoPlay.Application.Ports;

/// <summary>
/// Raised by persistence adapters when data cannot be read or written, whatever the underlying technology,
/// so that the application never depends on adapter-specific exceptions.
/// </summary>
public sealed class PersistenceException : Exception
{
    public PersistenceException()
    {
    }

    public PersistenceException(string message)
        : base(message)
    {
    }

    public PersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
