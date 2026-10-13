using System.Collections.ObjectModel;
using AutoPlay.Presentation.Services;
using AutoPlay.Application.UseCases;
using AutoPlay.Domain.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoPlay.Presentation.ViewModels;

public sealed partial class MainViewModel(
    ProfileService profileService,
    ScreenService screenService,
    SequenceService sequenceService,
    IScreenRecordingFlow screenRecording,
    ISequenceEditingFlow sequenceEditing,
    ISequenceExecutionFlow sequenceExecution,
    IUserDialogs dialogs,
    IShellLauncher shell,
    AppPaths paths,
    RunScheduler? scheduler = null,
    ISettingsFlow? settingsFlow = null,
    CaptureInboxService? captureInbox = null,
    SequenceExecutionService? executionService = null,
    Func<DateTimeOffset>? now = null) : ObservableObject
{
    /// <summary>Size of the screen thumbnails, in pixels.</summary>
    private const int ThumbnailWidth = 96;
    private const int ThumbnailHeight = 54;

    private readonly Func<DateTimeOffset> _now = now ?? (() => DateTimeOffset.Now);

    /// <summary>Quick captures of the selected profile waiting to be annotated, oldest first.</summary>
    public ObservableCollection<PendingCapture> PendingCaptures { get; } = [];

    public bool HasPendingCaptures => PendingCaptures.Count > 0;

    /// <summary>
    /// Captures the last target region of the selected profile into the pending captures (quick capture hotkey).
    /// Returns true if a capture was added.
    /// </summary>
    public bool QuickCapture()
    {
        if (captureInbox is null)
        {
            return false;
        }

        if (SelectedProfile is not { } profile)
        {
            StatusMessage = "Quick capture: select a profile first.";
            return false;
        }

        var result = captureInbox.QuickCapture(profile);
        if (!result.Succeeded)
        {
            StatusMessage = $"Quick capture: {string.Join(" ", result.Errors)}";
            return false;
        }

        ReloadPendingCaptures();
        StatusMessage = $"Screen captured ({PendingCaptures.Count} pending capture(s) to annotate).";
        return true;
    }

    [RelayCommand]
    private void AnnotateCapture(PendingCapture? capture)
    {
        if (captureInbox is null || SelectedProfile is not { } profile || capture is null)
        {
            return;
        }

        var knownIds = Screens.Select(s => s.Id).ToHashSet();
        if (!screenRecording.AnnotateCapture(profile, captureInbox.GetImage(profile.Id, capture.Id)))
        {
            return;
        }

        captureInbox.Discard(profile.Id, capture.Id);
        ReloadPendingCaptures();
        ReloadScreens();
        var created = Screens.FirstOrDefault(s => !knownIds.Contains(s.Id));
        SelectedScreen = created;
        StatusMessage = created is null ? "Screen saved." : $"Screen '{created.Name}' saved.";
    }

    [RelayCommand]
    private void DiscardCapture(PendingCapture? capture)
    {
        if (captureInbox is null || SelectedProfile is not { } profile || capture is null)
        {
            return;
        }

        var result = captureInbox.Discard(profile.Id, capture.Id);
        if (!result.Succeeded)
        {
            dialogs.ShowError(string.Join(Environment.NewLine, result.Errors));
        }

        ReloadPendingCaptures();
    }

    private void ReloadPendingCaptures()
    {
        PendingCaptures.Clear();
        if (captureInbox is not null && SelectedProfile is { } profile)
        {
            foreach (var capture in captureInbox.GetPendingCaptures(profile.Id))
            {
                PendingCaptures.Add(capture);
            }
        }

        OnPropertyChanged(nameof(HasPendingCaptures));
    }

    [RelayCommand]
    private void OpenSettings()
    {
        if (settingsFlow?.Open(SelectedProfile) == true)
        {
            StatusMessage = "Settings saved.";
        }
    }

    /// <summary>Shows the scheduler messages in the status bar.</summary>
    public void AttachScheduler()
    {
        if (scheduler is not null)
        {
            scheduler.Notification += (_, message) =>
            {
                StatusMessage = message;
                ReloadSequences(SelectedSequence?.Id); // Shows the outcome of a scheduled run.
            };
        }
    }

    [RelayCommand]
    private void OpenLogsFolder() => shell.Open(paths.Logs);

    [RelayCommand]
    private void OpenDataFolder() => shell.Open(paths.Profiles);

    public ObservableCollection<Profile> Profiles { get; } = [];

    public ObservableCollection<ScreenItem> Screens { get; } = [];

    public ObservableCollection<SequenceItem> Sequences { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteProfileCommand), nameof(ExportProfileCommand), nameof(RecordScreenCommand), nameof(NewSequenceCommand))]
    public partial Profile? SelectedProfile { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditScreenCommand), nameof(RecaptureScreenCommand), nameof(DeleteScreenCommand))]
    public partial ScreenItem? SelectedScreen { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditSequenceCommand), nameof(DuplicateSequenceCommand), nameof(DeleteSequenceCommand), nameof(RunSequenceCommand))]
    public partial SequenceItem? SelectedSequence { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateProfileCommand))]
    public partial string NewProfileName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Ready";

    public void Load()
    {
        Profiles.Clear();
        foreach (var profile in profileService.GetProfiles())
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
        ReloadPendingCaptures();
    }

    private void ReloadSequences(Guid? sequenceToSelect = null)
    {
        Sequences.Clear();
        if (SelectedProfile is null)
        {
            return;
        }

        var lastRuns = executionService?.GetLastRuns(SelectedProfile.Id) ?? new Dictionary<Guid, AutoPlay.Application.Execution.RunSummary>();
        foreach (var sequence in sequenceService.GetSequences(SelectedProfile.Id))
        {
            Sequences.Add(new SequenceItem(sequence, lastRuns.GetValueOrDefault(sequence.Id), _now()));
        }

        SelectedSequence = Sequences.FirstOrDefault(s => s.Id == sequenceToSelect);
    }

    private bool CanCreateSequence() => SelectedProfile is not null;

    [RelayCommand(CanExecute = nameof(CanCreateSequence))]
    private void NewSequence()
    {
        if (SelectedProfile is not { } profile)
        {
            return;
        }

        var knownIds = Sequences.Select(s => s.Id).ToHashSet();
        if (sequenceEditing.Edit(profile, null))
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
        if (SelectedProfile is not { } profile || SelectedSequence?.Sequence is not { } sequence)
        {
            return;
        }

        if (sequenceEditing.Edit(profile, sequence))
        {
            ReloadSequences(sequence.Id);
            StatusMessage = $"Sequence '{SelectedSequence?.Name}' saved.";
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSequence))]
    private async Task RunSequenceAsync()
    {
        if (SelectedProfile is not { } profile || SelectedSequence?.Sequence is not { } sequence)
        {
            return;
        }

        await sequenceExecution.RunAsync(profile, sequence);
        ReloadSequences(sequence.Id);
        StatusMessage = SelectedSequence?.LastRun is null
            ? $"Sequence '{sequence.Name}' not run."
            : $"Sequence '{sequence.Name}': {SelectedSequence.LastRunText}.";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSequence))]
    private void DuplicateSequence()
    {
        if (SelectedProfile is not { } profile || SelectedSequence?.Sequence is not { } sequence)
        {
            return;
        }

        var result = sequenceService.Duplicate(profile.Id, sequence.Id);
        if (!result.Succeeded || result.Value is not { } copy)
        {
            dialogs.ShowError(string.Join(Environment.NewLine, result.Errors));
            return;
        }

        ReloadSequences(copy.Id);
        StatusMessage = $"Sequence '{copy.Name}' created.";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSequence))]
    private void DeleteSequence()
    {
        if (SelectedProfile is not { } profile || SelectedSequence?.Sequence is not { } sequence)
        {
            return;
        }

        var callers = sequenceService.GetCallers(profile.Id, sequence.Id);
        var warning = callers.Count == 0
            ? string.Empty
            : $"{Environment.NewLine}{Environment.NewLine}It is run by: {string.Join(", ", callers.Select(c => c.Name))}. "
              + "The steps running it will have to be removed from these sequences.";
        if (!dialogs.Confirm($"Delete the sequence '{sequence.Name}'?{warning}", "AutoPlay"))
        {
            return;
        }

        sequenceService.Delete(profile.Id, sequence.Id);
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

        foreach (var screen in screenService.GetScreens(SelectedProfile.Id))
        {
            Screens.Add(new ScreenItem(screen, screenService.GetThumbnail(SelectedProfile.Id, screen.Id, ThumbnailWidth, ThumbnailHeight)));
        }

        SelectedScreen = Screens.FirstOrDefault(s => s.Id == screenToSelect);
    }

    private bool CanRecordScreen() => SelectedProfile is not null;

    [RelayCommand(CanExecute = nameof(CanRecordScreen))]
    private async Task RecordScreenAsync()
    {
        if (SelectedProfile is not { } profile)
        {
            return;
        }

        var knownIds = Screens.Select(s => s.Id).ToHashSet();
        if (await screenRecording.RecordNewAsync(profile))
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
        if (SelectedProfile is not { } profile || SelectedScreen?.Screen is not { } screen)
        {
            return;
        }

        if (screenRecording.Edit(profile, screen))
        {
            ReloadScreens(screen.Id);
            StatusMessage = $"Screen '{SelectedScreen?.Name}' saved.";
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedScreen))]
    private async Task RecaptureScreenAsync()
    {
        if (SelectedProfile is not { } profile || SelectedScreen?.Screen is not { } screen)
        {
            return;
        }

        if (await screenRecording.RecaptureAsync(profile, screen))
        {
            ReloadScreens(screen.Id);
            StatusMessage = $"Screen '{screen.Name}' re-captured.";
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedScreen))]
    private void DeleteScreen()
    {
        if (SelectedProfile is not { } profile || SelectedScreen?.Screen is not { } screen)
        {
            return;
        }

        var usages = screenService.GetUsages(profile.Id, screen.Id);
        var warning = usages.Count == 0
            ? string.Empty
            : $"{Environment.NewLine}{Environment.NewLine}It is used by: {string.Join(", ", usages.Select(u => u.Name))}. "
              + "The steps using it will have to be removed from these sequences.";
        if (!dialogs.Confirm($"Delete the screen '{screen.Name}' and its locations?{warning}", "AutoPlay"))
        {
            return;
        }

        screenService.DeleteScreen(profile.Id, screen.Id);
        ReloadScreens();
        StatusMessage = $"Screen '{screen.Name}' deleted.";
    }

    private bool CanCreateProfile() =>
        !string.IsNullOrWhiteSpace(NewProfileName)
        && !Profiles.Any(p => string.Equals(p.Name, NewProfileName.Trim(), StringComparison.OrdinalIgnoreCase));

    [RelayCommand(CanExecute = nameof(CanCreateProfile))]
    private void CreateProfile()
    {
        var result = profileService.CreateProfile(NewProfileName);
        if (!result.Succeeded || result.Value is not { } profile)
        {
            dialogs.ShowError(string.Join(Environment.NewLine, result.Errors));
            return;
        }

        Profiles.Add(profile);
        SelectedProfile = profile;
        NewProfileName = string.Empty;
        StatusMessage = $"Profile '{profile.Name}' created.";
    }

    private bool CanDeleteProfile() => SelectedProfile is not null;

    [RelayCommand(CanExecute = nameof(CanDeleteProfile))]
    private void ExportProfile()
    {
        if (SelectedProfile is not { } profile)
        {
            return;
        }

        // Characters that Windows does not accept in file names (whatever the OS running this code).
        const string invalid = "\\/:*?\"<>|";
        var safeName = new string(profile.Name.Select(c => invalid.Contains(c, StringComparison.Ordinal) || char.IsControl(c) ? '_' : c).ToArray());
        var fileName = $"AutoPlay - {safeName}.zip";
        if (dialogs.AskSaveFilePath($"Export the profile '{profile.Name}'", fileName, "Zip archive (*.zip)|*.zip") is not { } path)
        {
            return;
        }

        var result = profileService.ExportProfile(profile.Id, path);
        if (result.Succeeded)
        {
            StatusMessage = $"Profile '{profile.Name}' exported to {path}.";
        }
        else
        {
            dialogs.ShowError(string.Join(Environment.NewLine, result.Errors));
        }
    }

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

        profileService.DeleteProfile(profile.Id);
        Profiles.Remove(profile);
        SelectedProfile = Profiles.FirstOrDefault();
        StatusMessage = $"Profile '{profile.Name}' deleted.";
    }
}
