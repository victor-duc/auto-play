using AutoPlay.Domain.Model;

namespace AutoPlay.Application.Ports;

/// <summary>Driven port: persistence of profiles.</summary>
public interface IProfileRepository
{
    IReadOnlyList<Profile> LoadProfiles();

    void SaveProfile(Profile profile);

    /// <summary>Deletes the profile with all its screens, sequences and logs.</summary>
    void DeleteProfile(Guid profileId);
}
