using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using AutoPlay.App.Imaging;
using AutoPlay.Domain.Imaging;

namespace AutoPlay.App.Converters;

/// <summary>Displays a <see cref="RawImage"/> exposed by a view model; the bitmap is created once per image.</summary>
public sealed class RawImageToBitmapConverter : IValueConverter
{
    private static readonly ConditionalWeakTable<RawImage, BitmapSource> Cache = [];

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is RawImage image ? Cache.GetValue(image, i => i.ToBitmapSource()) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
