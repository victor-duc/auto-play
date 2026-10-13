namespace AutoPlay.Application.Ports;

/// <summary>Driven port: exports a profile as a single archive file (backup or sharing).</summary>
public interface IProfileArchive
{
    /// <summary>
    /// Writes the profile, its screens (with their images) and its sequences to a .zip file at
    /// <paramref name="destinationPath"/>, replacing it if it exists. Diagnostic logs are not included.
    /// </summary>
    void ExportProfile(Guid profileId, string destinationPath);
}
