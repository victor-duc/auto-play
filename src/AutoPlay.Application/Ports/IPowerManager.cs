namespace AutoPlay.Application.Ports;

public interface IPowerManager
{
    /// <summary>Prevents the system from sleeping and the display from turning off until the result is disposed.</summary>
    IDisposable PreventSleep();
}
