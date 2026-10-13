namespace AutoPlay.Domain.Model;

/// <summary>A capture taken with the quick capture hotkey, waiting to be annotated as a screen or discarded.</summary>
public sealed record PendingCapture(Guid Id, DateTimeOffset CapturedAt);
