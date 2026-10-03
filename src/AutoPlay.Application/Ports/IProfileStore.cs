namespace AutoPlay.Application.Ports;

/// <summary>
/// Transitional aggregate of the persistence ports, kept while the UI still uses the storage directly.
/// </summary>
public interface IProfileStore : IProfileRepository, IScreenRepository, ISequenceRepository, IRunLogStore;
