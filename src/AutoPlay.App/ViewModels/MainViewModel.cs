using System.Collections.ObjectModel;
using AutoPlay.Core.Model;
using AutoPlay.Core.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoPlay.App.ViewModels;

public sealed partial class MainViewModel(IProfileStore store) : ObservableObject
{
    public ObservableCollection<Profile> Profiles { get; } = [];

    public ObservableCollection<Screen> Screens { get; } = [];

    public ObservableCollection<Sequence> Sequences { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteProfileCommand))]
    public partial Profile? SelectedProfile { get; set; }

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
        Screens.Clear();
        Sequences.Clear();
        if (value is null)
        {
            return;
        }

        foreach (var screen in store.LoadScreens(value.Id))
        {
            Screens.Add(screen);
        }

        foreach (var sequence in store.LoadSequences(value.Id))
        {
            Sequences.Add(sequence);
        }
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

        store.DeleteProfile(profile.Id);
        Profiles.Remove(profile);
        SelectedProfile = Profiles.FirstOrDefault();
        StatusMessage = $"Profile '{profile.Name}' deleted.";
    }
}
