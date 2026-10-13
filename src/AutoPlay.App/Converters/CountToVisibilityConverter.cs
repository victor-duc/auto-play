using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AutoPlay.App.Converters;

/// <summary>Visible when the bound count is 0 (to show an empty-state hint), collapsed otherwise.</summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
