using AutoPlay.Application.Ports;
using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoPlay.Application.UseCases;

/// <summary>Use cases on profiles.</summary>
public sealed class ProfileService(
    IProfileRepository profiles,
    IProfileArchive? archive = null,
    ILogger<ProfileService>? logger = null)
{
    private readonly ILogger _logger = (ILogger?)logger ?? NullLogger.Instance;

    public IReadOnlyList<Profile> GetProfiles() => profiles.LoadProfiles();

    /// <summary>Creates a profile; the name is required and must be unique (ignoring case and surrounding spaces).</summary>
    public OperationResult<Profile> CreateProfile(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return OperationResult.Failure<Profile>("The profile name is required.");
        }

        try
        {
            if (profiles.LoadProfiles().Any(p => string.Equals(p.Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return OperationResult.Failure<Profile>($"Another profile is already named '{trimmed}'.");
            }

            var profile = new Profile { Name = trimmed };
            profiles.SaveProfile(profile);
            return OperationResult.Success(profile);
        }
        catch (PersistenceException ex)
        {
            UseCaseLog.StorageError(_logger, ex);
            return OperationResult.Failure<Profile>(ex.Message);
        }
    }

    /// <summary>Exports the profile (screens, images, sequences, without logs) to a .zip file.</summary>
    public OperationResult ExportProfile(Guid profileId, string destinationPath)
    {
        if (archive is null)
        {
            return OperationResult.Failure("Export is not available.");
        }

        try
        {
            archive.ExportProfile(profileId, destinationPath);
            return OperationResult.Success();
        }
        catch (PersistenceException ex)
        {
            UseCaseLog.StorageError(_logger, ex);
            return OperationResult.Failure(ex.Message);
        }
    }

    /// <summary>Deletes the profile with all its screens and sequences.</summary>
    public void DeleteProfile(Guid profileId) => profiles.DeleteProfile(profileId);

    /// <summary>Remembers the target region, so that the framing overlay reopens there next time.</summary>
    public void RememberTargetRegion(Profile profile, PixelRect targetRegion)
    {
        profile.LastTargetRegion = targetRegion;
        profiles.SaveProfile(profile);
    }
}
