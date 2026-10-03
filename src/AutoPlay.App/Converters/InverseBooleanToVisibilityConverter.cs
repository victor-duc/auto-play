using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AutoPlay.App.Converters;

/// <summary>Collapsed when the value is true, visible otherwise.</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
