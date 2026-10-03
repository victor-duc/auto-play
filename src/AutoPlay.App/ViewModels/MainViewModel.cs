using System.Collections.ObjectModel;
using AutoPlay.App.Services;
using AutoPlay.Domain.Model;
using AutoPlay.Domain.Sequencing;
using AutoPlay.Application.Ports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoPlay.App.ViewModels;

public sealed partial class MainViewModel(
    IProfileStore store,
    ScreenRecordingFlow screenRecording,
    SequenceEditingFlow sequenceEditing,
    SequenceExecutionFlow sequenceExecution,
    IUserDialogs dialogs) : ObservableObject
{
    public ObservableCollection<Profile> Profiles { get; } = [];

    public ObservableCollection<Screen> Screens { get; } = [];

    public ObservableCollection<Sequence> Sequences { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteProfileCommand), nameof(RecordScreenCommand), nameof(NewSequenceCommand))]
    public partial Profile? SelectedProfile { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditScreenCommand), nameof(DeleteScreenCommand))]
    public partial Screen? SelectedScreen { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditSequenceCommand), nameof(DuplicateSequenceCommand), nameof(DeleteSequenceCommand), nameof(RunSequenceCommand))]
    public partial Sequence? SelectedSequence { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateProfileCommand))]
    public partial string NewProfileName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Ready";

    public void Load()
    {
        Profiles.Clear();
        foreach (var profile in store.LoadProfiles())
        {
            Profiles.Add(profile);
        }

        SelectedProfile = Profiles.FirstOrDefault();
        StatusMessage = $"{Profiles.Count} profile(s) loaded.";
    }

    partial void OnSelectedProfileChanged(Profile? value)
    {
        ReloadScreens();
        ReloadSequences();
    }

    private void ReloadSequences(Guid? sequenceToSelect = null)
    {
        Sequences.Clear();
        if (SelectedProfile is null)
        {
            return;
        }

        foreach (var sequence in store.LoadSequences(SelectedProfile.Id))
        {
            Sequences.Add(sequence);
        }

        SelectedSequence = Sequences.FirstOrDefault(s => s.Id == sequenceToSelect);
    }

    private List<string> OtherSequenceNames(Sequence? except) =>
        Sequences.Where(s => s.Id != except?.Id).Select(s => s.Name).ToList();

    private bool CanCreateSequence() => SelectedProfile is not null;

    [RelayCommand(CanExecute = nameof(CanCreateSequence))]
    private void NewSequence()
    {
        if (SelectedProfile is not { } profile)
        {
            return;
        }

        var knownIds = Sequences.Select(s => s.Id).ToHashSet();
        if (sequenceEditing.Edit(profile, [.. Screens], null, OtherSequenceNames(null)))
        {
            ReloadSequences();
            SelectedSequence = Sequences.FirstOrDefault(s => !knownIds.Contains(s.Id));
            StatusMessage = $"Sequence '{SelectedSequence?.Name}' saved.";
        }
    }

    private bool HasSelectedSequence() => SelectedSequence is not null;

    [RelayCommand(CanExecute = nameof(HasSelectedSequence))]
    private void EditSequence()
    {
        if (SelectedProfile is not { } profile || SelectedSequence is not { } sequence)
        {
            return;
        }

        if (sequenceEditing.Edit(profile, [.. Screens], sequence, OtherSequenceNames(sequence)))
        {
            ReloadSequences(sequence.Id);
            StatusMessage = $"Sequence '{SelectedSequence?.Name}' saved.";
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSequence))]
    private async Task RunSequenceAsync()
    {
        if (SelectedProfile is not { } profile || SelectedSequence is not { } sequence)
        {
            return;
        }

        await sequenceExecution.RunAsync(profile, sequence);
        StatusMessage = $"Sequence '{sequence.Name}' finished.";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSequence))]
    private void DuplicateSequence()
    {
        if (SelectedProfile is not { } profile || SelectedSequence is not { } sequence)
        {
            return;
        }

        var names = Sequences.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = $"{sequence.Name} (copy)";
        for (var i = 2; names.Contains(name); i++)
        {
            name = $"{sequence.Name} (copy {i})";
        }

        var copy = new Sequence
        {
            Name = name,
            RepeatCount = sequence.RepeatCount,
            Steps = sequence.Steps
                .Select(s => new SequenceStep
                {
                    ScreenId = s.ScreenId,
                    LocationId = s.LocationId,
                    Delay = s.Delay,
                    VerificationTimeoutMs = s.VerificationTimeoutMs,
                    Verify = s.Verify,
                })
                .ToList(),
        };
        store.SaveSequence(profile.Id, copy);
        ReloadSequences(copy.Id);
        StatusMessage = $"Sequence '{copy.Name}' created.";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSequence))]
    private void DeleteSequence()
    {
        if (SelectedProfile is not { } profile || SelectedSequence is not { } sequence)
        {
            return;
        }

        if (!dialogs.Confirm($"Delete the sequence '{sequence.Name}'?", "AutoPlay"))
        {
            return;
        }

        store.DeleteSequence(profile.Id, sequence.Id);
        ReloadSequences();
        StatusMessage = $"Sequence '{sequence.Name}' deleted.";
    }

    private void ReloadScreens(Guid? screenToSelect = null)
    {
        Screens.Clear();
        if (SelectedProfile is null)
        {
            return;
        }

        foreach (var screen in store.LoadScreens(SelectedProfile.Id))
        {
            Screens.Add(screen);
        }

        SelectedScreen = Screens.FirstOrDefault(s => s.Id == screenToSelect);
    }

    private List<string> OtherScreenNames(Screen? except) =>
        Screens.Where(s => s.Id != except?.Id).Select(s => s.Name).ToList();

    private bool CanRecordScreen() => SelectedProfile is not null;

    [RelayCommand(CanExecute = nameof(CanRecordScreen))]
    private async Task RecordScreenAsync()
    {
        if (SelectedProfile is not { } profile)
        {
            return;
        }

        var knownIds = Screens.Select(s => s.Id).ToHashSet();
        if (await screenRecording.RecordNewAsync(profile, OtherScreenNames(null)))
        {
            ReloadScreens();
            var created = Screens.FirstOrDefault(s => !knownIds.Contains(s.Id));
            SelectedScreen = created;
            StatusMessage = created is null ? "Screen saved." : $"Screen '{created.Name}' saved.";
        }
    }

    private bool HasSelectedScreen() => SelectedScreen is not null;

    [RelayCommand(CanExecute = nameof(HasSelectedScreen))]
    private void EditScreen()
    {
        if (SelectedProfile is not { } profile || SelectedScreen is not { } screen)
        {
            return;
        }

        if (screenRecording.Edit(profile, screen, OtherScreenNames(screen)))
        {
            ReloadScreens(screen.Id);
            StatusMessage = $"Screen '{SelectedScreen?.Name}' saved.";
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedScreen))]
    private void DeleteScreen()
    {
        if (SelectedProfile is not { } profile || SelectedScreen is not { } screen)
        {
            return;
        }

        var usages = SequenceValidator.FindUsages(Sequences, screen.Id);
        var warning = usages.Count == 0
            ? string.Empty
            : $"{Environment.NewLine}{Environment.NewLine}It is used by: {string.Join(", ", usages.Select(u => u.Name))}. "
              + "The steps using it will have to be removed from these sequences.";
        if (!dialogs.Confirm($"Delete the screen '{screen.Name}' and its locations?{warning}", "AutoPlay"))
        {
            return;
        }

        store.DeleteScreen(profile.Id, screen.Id);
        ReloadScreens();
        StatusMessage = $"Screen '{screen.Name}' deleted.";
    }

    private bool CanCreateProfile() =>
        !string.IsNullOrWhiteSpace(NewProfileName)
        && !Profiles.Any(p => string.Equals(p.Name, NewProfileName.Trim(), StringComparison.OrdinalIgnoreCase));

    [RelayCommand(CanExecute = nameof(CanCreateProfile))]
    private void CreateProfile()
    {
        var profile = new Profile { Name = NewProfileName.Trim() };
        store.SaveProfile(profile);
        Profiles.Add(profile);
        SelectedProfile = profile;
        NewProfileName = string.Empty;
        StatusMessage = $"Profile '{profile.Name}' created.";
    }

    private bool CanDeleteProfile() => SelectedProfile is not null;

    [RelayCommand(CanExecute = nameof(CanDeleteProfile))]
    private void DeleteProfile()
    {
        if (SelectedProfile is not { } profile)
        {
            return;
        }

        if (!dialogs.Confirm($"Delete the profile '{profile.Name}' with all its screens and sequences?", "AutoPlay"))
        {
            return;
        }

        store.DeleteProfile(profile.Id);
        Profiles.Remove(profile);
        SelectedProfile = Profiles.FirstOrDefault();
        StatusMessage = $"Profile '{profile.Name}' deleted.";
    }
}
