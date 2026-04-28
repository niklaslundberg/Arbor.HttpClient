using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Arbor.HttpClient.Desktop.Features.Environments;

/// <summary>
/// Returns white (<see cref="Brushes.White"/>) when the bound value is a non-empty hex color string
/// so that text on a colored environment dropdown meets WCAG 2.1 AA contrast (≥ 4.5:1).
/// Returns <see langword="null"/> when the value is null or empty, allowing the control to fall
/// back to its default foreground.
/// </summary>
public sealed class HexColorToForegroundConverter : IValueConverter
{
    public static readonly HexColorToForegroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string hex && !string.IsNullOrWhiteSpace(hex) ? Brushes.White : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
