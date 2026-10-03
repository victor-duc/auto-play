using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using AutoPlay.App.Imaging;
using AutoPlay.App.Services;
using AutoPlay.Domain.Model;
using AutoPlay.Domain.Sequencing;
using AutoPlay.Application.Ports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoPlay.App.ViewModels;

/// <summary>Creates or edits a sequence of a profile.</summary>
public sealed partial class SequenceEditorViewModel : ObservableObject
{
    private readonly IProfileStore _store;
    private readonly IUserDialogs _dialogs;
    private readonly Profile _profile;
    private readonly IReadOnlyList<Screen> _screens;
    private readonly IReadOnlyList<string> _otherSequenceNames;
    private readonly Guid _sequenceId;

    /// <param name="screens">The screens of the profile.</param>
    /// <param name="existingSequence">The sequence to edit, or null to create a new one.</param>
    /// <param name="otherSequenceNames">Names of the other sequences of the profile, which must not be reused.</param>
    public SequenceEditorViewModel(
        IProfileStore store,
        IUserDialogs dialogs,
        Profile profile,
        IReadOnlyList<Screen> screens,
        Sequence? existingSequence,
        IReadOnlyList<string> otherSequenceNames)
    {
        _store = store;
        _dialogs = dialogs;
        _profile = profile;
        _screens = screens;
        _otherSequenceNames = otherSequenceNames;
        _sequenceId = existingSequence?.Id ?? Guid.NewGuid();

        Title = existingSequence is null ? "AutoPlay — New sequence" : $"AutoPlay — Edit sequence '{existingSequence.Name}'";
        Name = existingSequence?.Name ?? string.Empty;
        RepeatCountText = (existingSequence?.RepeatCount ?? 1).ToString(CultureInfo.CurrentCulture);

        ScreenChoices = screens
            .Select(screen => new ScreenChoice(
                screen,
                screen.Locations.Select(location => new LocationChoice(screen, location, LoadThumbnail(screen, location))).ToList()))
            .ToList();
        SelectedScreenChoice = ScreenChoices.Count > 0 ? ScreenChoices[0] : null;

        foreach (var step in existingSequence?.Steps ?? [])
        {
            AddStepViewModel(Steps.Count, new StepViewModel(step, FindChoice(step), profile.Defaults));
        }

        Steps.CollectionChanged += OnStepsChanged;
        Renumber();
        IsDirty = false;
    }

    /// <summary>Raised when the editor must close; the argument tells whether the sequence was saved.</summary>
    public event EventHandler<bool>? CloseRequested;

    public string Title { get; }

    public ObservableCollection<StepViewModel> Steps { get; } = [];

    public IReadOnlyList<ScreenChoice> ScreenChoices { get; }

    public bool HasScreens => ScreenChoices.Count > 0;

    public bool IsDirty { get; private set; }

    [ObservableProperty]
    public partial string Name { get; set; }

    /// <summary>Number of iterations, as typed by the user; 0 means until stopped.</summary>
    [ObservableProperty]
    public partial string RepeatCountText { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand), nameof(MoveDownCommand), nameof(DuplicateStepCommand), nameof(DeleteStepCommand))]
    public partial StepViewModel? SelectedStep { get; set; }

    [ObservableProperty]
    public partial ScreenChoice? SelectedScreenChoice { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddStepCommand))]
    public partial LocationChoice? SelectedLocationChoice { get; set; }

    private bool CanAddStep() => SelectedLocationChoice is not null;

    /// <summary>Adds the chosen location after the selected step (or at the end) and selects it.</summary>
    [RelayCommand(CanExecute = nameof(CanAddStep))]
    private void AddStep()
    {
        if (SelectedLocationChoice is not { } choice)
        {
            return;
        }

        var step = new StepViewModel(
            new SequenceStep { ScreenId = choice.Screen.Id, LocationId = choice.Location.Id },
            choice,
            _profile.Defaults);
        var index = SelectedStep is null ? Steps.Count : Steps.IndexOf(SelectedStep) + 1;
        AddStepViewModel(index, step);
        SelectedStep = step;
    }

    private bool CanMoveUp() => SelectedStep is not null && Steps.IndexOf(SelectedStep) > 0;

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp() => Move(-1);

    private bool CanMoveDown() => SelectedStep is not null && Steps.IndexOf(SelectedStep) < Steps.Count - 1;

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown() => Move(+1);

    private bool HasSelectedStep() => SelectedStep is not null;

    [RelayCommand(CanExecute = nameof(HasSelectedStep))]
    private void DuplicateStep()
    {
        if (SelectedStep is not { } step)
        {
            return;
        }

        var copy = step.Clone();
        AddStepViewModel(Steps.IndexOf(step) + 1, copy);
        SelectedStep = copy;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedStep))]
    private void DeleteStep()
    {
        if (SelectedStep is not { } step)
        {
            return;
        }

        var index = Steps.IndexOf(step);
        step.PropertyChanged -= OnStepPropertyChanged;
        Steps.Remove(step);
        SelectedStep = Steps.Count == 0 ? null : Steps[Math.Min(index, Steps.Count - 1)];
    }

    [RelayCommand]
    private void Save()
    {
        ErrorMessage = null;

        if (!int.TryParse(RepeatCountText.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var repeatCount))
        {
            ErrorMessage = "The repeat count must be a whole number (0 = until stopped).";
            return;
        }

        var steps = new List<SequenceStep>();
        foreach (var stepViewModel in Steps)
        {
            if (stepViewModel.TryToStep(out var step) is { } error)
            {
                ErrorMessage = error;
                return;
            }

            steps.Add(step);
        }

        var errors = SequenceValidator.Validate(Name, repeatCount, steps, _screens, _otherSequenceNames);
        if (errors.Count > 0)
        {
            ErrorMessage = string.Join(Environment.NewLine, errors);
            return;
        }

        var sequence = new Sequence { Id = _sequenceId, Name = Name.Trim(), RepeatCount = repeatCount, Steps = steps };
        try
        {
            _store.SaveSequence(_profile.Id, sequence);
        }
        catch (PersistenceException ex)
        {
            ErrorMessage = $"The sequence could not be saved: {ex.Message}";
            return;
        }

        IsDirty = false;
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        if (ConfirmDiscard())
        {
            IsDirty = false;
            CloseRequested?.Invoke(this, false);
        }
    }

    /// <summary>Asks the user to confirm losing unsaved changes; returns true if there are none.</summary>
    public bool ConfirmDiscard() =>
        !IsDirty || _dialogs.Confirm("Discard the changes made to this sequence?", "AutoPlay");

    partial void OnNameChanged(string value) => IsDirty = true;

    partial void OnRepeatCountTextChanged(string value) => IsDirty = true;

    partial void OnSelectedScreenChoiceChanged(ScreenChoice? value) =>
        SelectedLocationChoice = value is { Locations.Count: > 0 } ? value.Locations[0] : null;

    private void Move(int offset)
    {
        if (SelectedStep is not { } step)
        {
            return;
        }

        var index = Steps.IndexOf(step);
        Steps.Move(index, index + offset);
        SelectedStep = step;
    }

    private void AddStepViewModel(int index, StepViewModel step)
    {
        step.PropertyChanged += OnStepPropertyChanged;
        Steps.Insert(index, step);
    }

    private void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Renumber();
        IsDirty = true;
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    private void OnStepPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(StepViewModel.Number) or nameof(StepViewModel.Summary)))
        {
            IsDirty = true;
        }
    }

    private void Renumber()
    {
        for (var i = 0; i < Steps.Count; i++)
        {
            Steps[i].Number = i + 1;
        }
    }

    private LocationChoice? FindChoice(SequenceStep step) =>
        ScreenChoices.FirstOrDefault(s => s.Screen.Id == step.ScreenId)?.Locations.FirstOrDefault(l => l.Location.Id == step.LocationId);

    private System.Windows.Media.Imaging.BitmapSource? LoadThumbnail(Screen screen, Location location)
    {
        try
        {
            return _store.LoadLocationTemplate(_profile.Id, screen.Id, location.Id).ToBitmapSource();
        }
        catch (PersistenceException)
        {
            return null;
        }
    }
}
