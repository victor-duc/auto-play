using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using AutoPlay.App.Imaging;
using AutoPlay.App.Services;
using AutoPlay.Core.Geometry;
using AutoPlay.Core.Imaging;
using AutoPlay.Core.Model;
using AutoPlay.Core.Recording;
using AutoPlay.Core.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoPlay.App.ViewModels;

/// <summary>Edits the locations of a screen on its frozen capture, for a new or an existing screen.</summary>
public sealed partial class ScreenEditorViewModel : ObservableObject
{
    private readonly IProfileStore _store;
    private readonly IUserDialogs _dialogs;
    private readonly Guid _profileId;
    private readonly Guid _screenId;
    private readonly RawImage _capture;
    private readonly IReadOnlyList<string> _otherScreenNames;

    /// <param name="existingScreen">The screen to edit, or null to create a new one.</param>
    /// <param name="otherScreenNames">Names of the other screens of the profile, which must not be reused.</param>
    public ScreenEditorViewModel(
        IProfileStore store,
        IUserDialogs dialogs,
        Guid profileId,
        RawImage capture,
        Screen? existingScreen,
        IReadOnlyList<string> otherScreenNames)
    {
        _store = store;
        _dialogs = dialogs;
        _profileId = profileId;
        _capture = capture;
        _otherScreenNames = otherScreenNames;
        _screenId = existingScreen?.Id ?? Guid.NewGuid();

        CaptureBitmap = capture.ToBitmapSource();
        var defaultDelay = existingScreen?.DefaultDelay ?? new Screen { Name = string.Empty }.DefaultDelay;
        ScreenName = existingScreen?.Name ?? string.Empty;
        DelayMinMs = defaultDelay.MinMs.ToString(CultureInfo.CurrentCulture);
        DelayMaxMs = defaultDelay.MaxMs.ToString(CultureInfo.CurrentCulture);
        Title = existingScreen is null ? "AutoPlay — New screen" : $"AutoPlay — Edit screen '{existingScreen.Name}'";

        if (existingScreen is not null)
        {
            foreach (var draft in ScreenBuilder.ToDrafts(existingScreen, capture.Size))
            {
                AddLocationViewModel(new LocationViewModel(draft));
            }
        }

        IsDirty = false;
    }

    /// <summary>Raised when the editor must close; the argument tells whether the screen was saved.</summary>
    public event EventHandler<bool>? CloseRequested;

    public string Title { get; }

    public BitmapSource CaptureBitmap { get; }

    public PixelSize CaptureSize => _capture.Size;

    public int PixelWidth => _capture.Width;

    public int PixelHeight => _capture.Height;

    public ObservableCollection<LocationViewModel> Locations { get; } = [];

    public bool IsDirty { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteLocationCommand))]
    public partial LocationViewModel? SelectedLocation { get; set; }

    [ObservableProperty]
    public partial string ScreenName { get; set; }

    [ObservableProperty]
    public partial string DelayMinMs { get; set; }

    [ObservableProperty]
    public partial string DelayMaxMs { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>
    /// Ratio between displayed and capture pixels. Overlay strokes and labels are divided by it
    /// so that they keep a constant on-screen size whatever the zoom.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StrokeThickness), nameof(MarkerScale), nameof(HandleTolerance))]
    public partial double DisplayScale { get; set; } = 1;

    public double StrokeThickness => 2 / DisplayScale;

    public double MarkerScale => 1 / DisplayScale;

    /// <summary>Distance, in capture pixels, within which a corner can be grabbed.</summary>
    public int HandleTolerance => (int)Math.Ceiling(6 / DisplayScale);

    /// <summary>Adds a location with the given rectangle, a default name and a centered click point, and selects it.</summary>
    public LocationViewModel AddLocation(PixelRect bounds)
    {
        var number = Locations.Count + 1;
        while (Locations.Any(l => string.Equals(l.Name, $"Location {number}", StringComparison.OrdinalIgnoreCase)))
        {
            number++;
        }

        var location = new LocationViewModel(new LocationDraft
        {
            Name = $"Location {number}",
            Bounds = bounds,
            ClickPoint = bounds.Center,
        });
        AddLocationViewModel(location);
        SelectedLocation = location;
        IsDirty = true;
        return location;
    }

    private bool CanDeleteLocation(LocationViewModel? location) => (location ?? SelectedLocation) is not null;

    [RelayCommand(CanExecute = nameof(CanDeleteLocation))]
    private void DeleteLocation(LocationViewModel? location)
    {
        location ??= SelectedLocation;
        if (location is null)
        {
            return;
        }

        location.PropertyChanged -= OnLocationPropertyChanged;
        Locations.Remove(location);
        if (SelectedLocation == location)
        {
            SelectedLocation = null;
        }

        IsDirty = true;
    }

    [RelayCommand]
    private void Save()
    {
        ErrorMessage = null;

        if (!TryParseDelay(out var delay, out var delayError))
        {
            ErrorMessage = delayError;
            return;
        }

        var drafts = new List<LocationDraft>();
        foreach (var location in Locations)
        {
            if (location.TryToDraft(out var draft) is { } error)
            {
                ErrorMessage = error;
                return;
            }

            drafts.Add(draft);
        }

        var errors = ScreenBuilder.Validate(ScreenName, drafts, CaptureSize, _otherScreenNames);
        if (errors.Count > 0)
        {
            ErrorMessage = string.Join(Environment.NewLine, errors);
            return;
        }

        var (screen, templates) = ScreenBuilder.Build(_screenId, ScreenName, delay, _capture, drafts);
        try
        {
            _store.SaveScreen(_profileId, screen, _capture, templates);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = $"The screen could not be saved: {ex.Message}";
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
        !IsDirty || _dialogs.Confirm("Discard the changes made to this screen?", "AutoPlay");

    partial void OnSelectedLocationChanged(LocationViewModel? oldValue, LocationViewModel? newValue)
    {
        oldValue?.IsSelected = false;
        newValue?.IsSelected = true;
    }

    partial void OnScreenNameChanged(string value) => IsDirty = true;

    partial void OnDelayMinMsChanged(string value) => IsDirty = true;

    partial void OnDelayMaxMsChanged(string value) => IsDirty = true;

    private void AddLocationViewModel(LocationViewModel location)
    {
        location.PropertyChanged += OnLocationPropertyChanged;
        Locations.Add(location);
    }

    private void OnLocationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LocationViewModel.IsSelected))
        {
            IsDirty = true;
        }
    }

    private bool TryParseDelay(out DelayRange delay, out string? error)
    {
        delay = null!;
        error = null;
        if (!int.TryParse(DelayMinMs.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var min)
            || !int.TryParse(DelayMaxMs.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var max))
        {
            error = "The delays must be whole numbers of milliseconds.";
            return false;
        }

        if (min < 0 || max < min)
        {
            error = "The delays must be positive, and the maximum must not be lower than the minimum.";
            return false;
        }

        delay = new DelayRange(min, max);
        return true;
    }
}
