using System.Windows.Media.Imaging;
using AutoPlay.Core.Model;

namespace AutoPlay.App.ViewModels;

/// <summary>A location that can be added to a sequence, with its template image.</summary>
public sealed record LocationChoice(Screen Screen, Location Location, BitmapSource? Thumbnail)
{
    public string Name => Location.Name;

    public string FullName => $"{Screen.Name} / {Location.Name}";
}

/// <summary>A screen and its locations, as offered in the "Add a step" panel.</summary>
public sealed record ScreenChoice(Screen Screen, IReadOnlyList<LocationChoice> Locations)
{
    public string Name => Screen.Name;
}
