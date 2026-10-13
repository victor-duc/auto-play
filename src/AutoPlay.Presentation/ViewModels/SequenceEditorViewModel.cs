using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using AutoPlay.Presentation.Services;
using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Model;
using AutoPlay.Application.Ports;
using AutoPlay.Application.UseCases;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoPlay.Presentation.ViewModels;

/// <summary>Creates or edits a sequence of a profile.</summary>
public sealed partial class SequenceEditorViewModel : ObservableObject
{
    private readonly SequenceService _sequenceService;
    private readonly ScreenService _screenService;
    private readonly IUserDialogs _dialogs;
    private readonly Profile _profile;
    private readonly Guid _sequenceId;

    /// <param name="screens">The screens of the profile.</param>
    /// <param name="existingSequence">The sequence to edit, or null to create a new one.</param>
    public SequenceEditorViewModel(
        SequenceService sequenceService,
        ScreenService screenService,
        IUserDialogs dialogs,
        Profile profile,
        IReadOnlyList<Screen> screens,
        Sequence? existingSequence)
    {
        _sequenceService = sequenceService;
        _screenService = screenService;
        _dialogs = dialogs;
        _profile = profile;
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

        SequenceChoices = LoadOtherSequences(existingSequence?.Id);
        SelectedSequenceChoice = SequenceChoices.Count > 0 ? SequenceChoices[0] : null;

        foreach (var step in existingSequence?.Steps ?? [])
        {
            AddStepViewModel(Steps.Count, new StepViewModel(step, FindChoice(step), FindSequenceChoice(step), profile.Defaults));
        }

        var schedule = existingSequence?.Schedule;
        ScheduleEnabled = schedule?.Enabled == true;
        ScheduleTimeText = (schedule?.Time ?? new TimeOnly(8, 0)).ToString("HH:mm", CultureInfo.InvariantCulture);
        Days = [.. new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }
            .Select(day => new DayToggle(day, schedule?.Days.Contains(day) == true))];
        foreach (var day in Days)
        {
            day.PropertyChanged += (_, _) => IsDirty = true;
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

    /// <summary>The other sequences of the profile, which a step can run.</summary>
    public IReadOnlyList<SequenceChoice> SequenceChoices { get; }

    public bool HasOtherSequences => SequenceChoices.Count > 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddRunSequenceCommand))]
    public partial SequenceChoice? SelectedSequenceChoice { get; set; }

    /// <summary>Whether the sequence starts automatically at <see cref="ScheduleTimeText"/>.</summary>
    [ObservableProperty]
    public partial bool ScheduleEnabled { get; set; }

    /// <summary>Time of the automatic start, as typed (HH:mm).</summary>
    [ObservableProperty]
    public partial string ScheduleTimeText { get; set; }

    /// <summary>Days of the automatic start; none checked means every day.</summary>
    public IReadOnlyList<DayToggle> Days { get; }

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
            null,
            _profile.Defaults);
        Insert(step);
    }

    private bool CanAddRunSequence() => SelectedSequenceChoice is not null;

    /// <summary>Adds a step running the chosen sequence after the selected step (or at the end).</summary>
    [RelayCommand(CanExecute = nameof(CanAddRunSequence))]
    private void AddRunSequence()
    {
        if (SelectedSequenceChoice is { } choice)
        {
            Insert(new StepViewModel(
                new SequenceStep { Kind = StepKind.RunSequence, SequenceId = choice.Id }, null, choice, _profile.Defaults));
        }
    }

    /// <summary>Adds a one-second wait after the selected step (or at the end) and selects it.</summary>
    [RelayCommand]
    private void AddWait() =>
        Insert(new StepViewModel(SequenceStep.Wait(new DelayRange(StepViewModel.DefaultWaitMs, StepViewModel.DefaultWaitMs)), null, null, _profile.Defaults));

    private void Insert(StepViewModel step)
    {
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

        if (!TryBuildSchedule(out var schedule, out var scheduleError))
        {
            ErrorMessage = scheduleError;
            return;
        }

        var sequence = new Sequence { Id = _sequenceId, Name = Name, RepeatCount = repeatCount, Steps = steps, Schedule = schedule };
        var result = _sequenceService.Save(_profile.Id, sequence);
        if (!result.Succeeded)
        {
            ErrorMessage = string.Join(Environment.NewLine, result.Errors);
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

    partial void OnScheduleEnabledChanged(bool value) => IsDirty = true;

    partial void OnScheduleTimeTextChanged(string value) => IsDirty = true;

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

    private SequenceChoice? FindSequenceChoice(SequenceStep step) =>
        step.Kind == StepKind.RunSequence ? SequenceChoices.FirstOrDefault(c => c.Id == step.SequenceId) : null;

    private List<SequenceChoice> LoadOtherSequences(Guid? currentId)
    {
        try
        {
            return _sequenceService.GetSequences(_profile.Id)
                .Where(s => s.Id != currentId)
                .Select(s => new SequenceChoice(s.Id, s.Name))
                .ToList();
        }
        catch (PersistenceException)
        {
            return [];
        }
    }

    /// <summary>
    /// Builds the schedule from the fields. A disabled schedule is kept (with its time and days) if the time is valid,
    /// so that it can be enabled again later.
    /// </summary>
    private bool TryBuildSchedule(out SequenceSchedule? schedule, out string? error)
    {
        schedule = null;
        error = null;
        var parsed = TimeOnly.TryParseExact(
            ScheduleTimeText.Trim(), ["H:mm", "HH:mm", "H:mm:ss", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time);
        if (!parsed)
        {
            if (ScheduleEnabled)
            {
                error = "The start time must be written as hours:minutes, for example 08:30.";
                return false;
            }

            return true;
        }

        schedule = new SequenceSchedule
        {
            Enabled = ScheduleEnabled,
            Time = time,
            Days = Days.Where(d => d.IsChecked).Select(d => d.Day).ToList(),
        };
        if (!ScheduleEnabled && schedule.Days.Count == 0 && time == new TimeOnly(8, 0))
        {
            schedule = null; // Untouched defaults: no schedule.
        }

        return true;
    }

    private LocationChoice? FindChoice(SequenceStep step) =>
        ScreenChoices.FirstOrDefault(s => s.Screen.Id == step.ScreenId)?.Locations.FirstOrDefault(l => l.Location.Id == step.LocationId);

    private RawImage? LoadThumbnail(Screen screen, Location location)
    {
        try
        {
            return _screenService.GetTemplate(_profile.Id, screen.Id, location.Id);
        }
        catch (PersistenceException)
        {
            return null;
        }
    }
}
