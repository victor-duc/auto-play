using AutoPlay.Domain.Imaging;

namespace AutoPlay.Application.Ports;

/// <summary>Driven port: storage of diagnostic images produced by runs.</summary>
public interface IRunLogStore
{
    /// <summary>Saves a diagnostic image for the profile and returns where it was stored.</summary>
    string SaveLogImage(Guid profileId, string name, RawImage image);
}
