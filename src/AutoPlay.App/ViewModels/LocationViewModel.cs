using System.Globalization;
using AutoPlay.Core.Geometry;
using AutoPlay.Core.Recording;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoPlay.App.ViewModels;

/// <summary>A location being edited on a capture, in capture pixel coordinates.</summary>
public sealed partial class LocationViewModel : ObservableObject
{
    public LocationViewModel(LocationDraft draft)
    {
        Id = draft.Id;
        Name = draft.Name;
        Bounds = draft.Bounds;
        ClickPoint = draft.ClickPoint;
        ClickPointIsCenter = draft.ClickPoint == draft.Bounds.Center;
        MatchThresholdText = draft.MatchThreshold?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
    }

    public Guid Id { get; }

    [ObservableProperty]
    public partial string Name { get; set; }

    /// <summary>Minimum match score, as typed by the user; empty means the profile default.</summary>
    [ObservableProperty]
    public partial string MatchThresholdText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(X), nameof(Y), nameof(Width), nameof(Height), nameof(ClickOffsetX), nameof(ClickOffsetY))]
    public partial PixelRect Bounds { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ClickOffsetX), nameof(ClickOffsetY))]
    public partial PixelPoint ClickPoint { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Whether the click point follows the center of the rectangle (until the user sets it explicitly).</summary>
    public bool ClickPointIsCenter { get; private set; }

    public int X => Bounds.Left;

    public int Y => Bounds.Top;

    public int Width => Bounds.Width;

    public int Height => Bounds.Height;

    /// <summary>Center of the click pixel, relative to the rectangle.</summary>
    public double ClickOffsetX => ClickPoint.X - Bounds.Left + 0.5;

    /// <summary>Center of the click pixel, relative to the rectangle.</summary>
    public double ClickOffsetY => ClickPoint.Y - Bounds.Top + 0.5;

    /// <summary>Moves the rectangle; the click point keeps its position relative to it.</summary>
    public void MoveTo(PixelRect bounds)
    {
        var offset = new PixelPoint(ClickPoint.X - Bounds.Left, ClickPoint.Y - Bounds.Top);
        Bounds = bounds;
        ClickPoint = ClickPointIsCenter ? bounds.Center : RectEditing.Clamp(new PixelPoint(bounds.Left + offset.X, bounds.Top + offset.Y), bounds);
    }

    /// <summary>Resizes the rectangle; the click point stays centered or is kept inside.</summary>
    public void ResizeTo(PixelRect bounds)
    {
        Bounds = bounds;
        ClickPoint = ClickPointIsCenter ? bounds.Center : RectEditing.Clamp(ClickPoint, bounds);
    }

    public void SetClickPoint(PixelPoint point)
    {
        ClickPointIsCenter = false;
        ClickPoint = RectEditing.Clamp(point, Bounds);
    }

    /// <summary>Converts to a draft; returns an error message if the threshold cannot be parsed.</summary>
    public string? TryToDraft(out LocationDraft draft)
    {
        double? threshold = null;
        var text = MatchThresholdText.Trim().Replace(',', '.');
        if (text.Length > 0)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                draft = null!;
                return $"The match threshold of '{Name}' is not a number.";
            }

            threshold = value;
        }

        draft = new LocationDraft { Id = Id, Name = Name, Bounds = Bounds, ClickPoint = ClickPoint, MatchThreshold = threshold };
        return null;
    }
}
