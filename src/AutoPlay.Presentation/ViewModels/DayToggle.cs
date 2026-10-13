using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoPlay.Presentation.ViewModels;

/// <summary>A day of the week that can be selected for a schedule.</summary>
public sealed partial class DayToggle(DayOfWeek day, bool isChecked) : ObservableObject
{
    public DayOfWeek Day { get; } = day;

    /// <summary>Short English name of the day (Mon, Tue…).</summary>
    public string Label { get; } = day.ToString()[..3];

    [ObservableProperty]
    public partial bool IsChecked { get; set; } = isChecked;
}
