namespace AutoPlay.Presentation.Services;

/// <summary>
/// Tracks whether the user is doing something that a scheduled run must not interrupt (recording or editing
/// a screen, editing a sequence, running a sequence).
/// </summary>
public sealed class ActivityTracker
{
    private int _count;

    public bool IsBusy => Volatile.Read(ref _count) > 0;

    /// <summary>Marks the application busy until the result is disposed.</summary>
    public IDisposable Begin()
    {
        Interlocked.Increment(ref _count);
        return new Activity(this);
    }

    private sealed class Activity(ActivityTracker owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Interlocked.Decrement(ref owner._count);
            }
        }
    }
}
