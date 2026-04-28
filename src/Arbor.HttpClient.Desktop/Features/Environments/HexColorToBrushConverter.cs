using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Arbor.HttpClient.Desktop.Features.Environments;

/// <summary>
/// Converts a hex color string (e.g. "#B41E1E") to a <see cref="SolidColorBrush"/>.
/// Returns <see langword="null"/> (transparent) when the value is null or empty,
/// leaving the control to use its default background.
/// </summary>
public sealed class HexColorToBrushConverter : IValueConverter
{
    public static readonly HexColorToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        return Color.TryParse(hex, out var color)
            ? new SolidColorBrush(color)
            : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
