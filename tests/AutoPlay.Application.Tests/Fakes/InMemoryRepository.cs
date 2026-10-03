using AutoPlay.Application.Ports;
using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Model;

namespace AutoPlay.Application.Tests.Fakes;

/// <summary>In-memory implementation of the persistence ports.</summary>
internal sealed class InMemoryRepository : IProfileRepository, IScreenRepository, ISequenceRepository, IRunLogStore
{
    public List<Profile> Profiles { get; } = [];

    public Dictionary<Guid, List<Screen>> Screens { get; } = [];

    public Dictionary<(Guid ScreenId, Guid LocationId), RawImage> Templates { get; } = [];

    public Dictionary<Guid, RawImage> Captures { get; } = [];

    public Dictionary<Guid, List<Sequence>> Sequences { get; } = [];

    public List<(string Name, RawImage Image)> LogImages { get; } = [];

    /// <summary>When set, every operation throws this error (simulates a storage failure).</summary>
    public PersistenceException? Failure { get; set; }

    public IReadOnlyList<Profile> LoadProfiles() => Check(Profiles.ToList());

    public void SaveProfile(Profile profile)
    {
        Check(0);
        Profiles.RemoveAll(p => p.Id == profile.Id);
        Profiles.Add(profile);
    }

    public void DeleteProfile(Guid profileId)
    {
        Check(0);
        Profiles.RemoveAll(p => p.Id == profileId);
    }

    public IReadOnlyList<Screen> LoadScreens(Guid profileId) => Check(ScreensOf(profileId).ToList());

    public void SaveScreen(Guid profileId, Screen screen, RawImage capture, IReadOnlyDictionary<Guid, RawImage> templates)
    {
        Check(0);
        ScreensOf(profileId).RemoveAll(s => s.Id == screen.Id);
        ScreensOf(profileId).Add(screen);
        Captures[screen.Id] = capture;
        foreach (var (locationId, template) in templates)
        {
            Templates[(screen.Id, locationId)] = template;
        }
    }

    public void DeleteScreen(Guid profileId, Guid screenId)
    {
        Check(0);
        ScreensOf(profileId).RemoveAll(s => s.Id == screenId);
    }

    public RawImage LoadScreenCapture(Guid profileId, Guid screenId) => Check(Captures[screenId]);

    public RawImage LoadLocationTemplate(Guid profileId, Guid screenId, Guid locationId) =>
        Check(Templates.TryGetValue((screenId, locationId), out var template)
            ? template
            : throw new PersistenceException("Missing template."));

    public IReadOnlyList<Sequence> LoadSequences(Guid profileId) => Check(SequencesOf(profileId).ToList());

    public void SaveSequence(Guid profileId, Sequence sequence)
    {
        Check(0);
        SequencesOf(profileId).RemoveAll(s => s.Id == sequence.Id);
        SequencesOf(profileId).Add(sequence);
    }

    public void DeleteSequence(Guid profileId, Guid sequenceId)
    {
        Check(0);
        SequencesOf(profileId).RemoveAll(s => s.Id == sequenceId);
    }

    public string SaveLogImage(Guid profileId, string name, RawImage image)
    {
        Check(0);
        LogImages.Add((name, image));
        return $"logs/{name}.png";
    }

    private List<Screen> ScreensOf(Guid profileId) =>
        Screens.TryGetValue(profileId, out var list) ? list : Screens[profileId] = [];

    private List<Sequence> SequencesOf(Guid profileId) =>
        Sequences.TryGetValue(profileId, out var list) ? list : Sequences[profileId] = [];

    private T Check<T>(T value) => Failure is { } failure ? throw failure : value;
}
