using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Arbor.HttpClient.Desktop.Features.HttpRequest;

/// <summary>
/// Maps an HTTP numeric status code to a color-family brush so the response status
/// line can be scanned at a glance (matches the convention used by Hoppscotch,
/// Insomnia, and Postman). Unknown / missing codes fall back to the default
/// method-fallback brush, which has a WCAG-verified contrast ratio against the
/// app's surfaces.
/// </summary>
public sealed class StatusCodeToColorConverter : IValueConverter
{
    public static readonly StatusCodeToColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var code = value switch
        {
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0
        };

        var brushKey = code switch
        {
            >= 200 and < 300 => "StatusSuccessBrush",
            >= 300 and < 400 => "StatusRedirectBrush",
            >= 400 and < 500 => "StatusClientErrorBrush",
            >= 500 and < 600 => "StatusServerErrorBrush",
            >= 100 and < 200 => "StatusInformationalBrush",
            _ => "MethodFallbackBrush"
        };

        return TryGetBrush(brushKey, out var brush) ? brush : Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static bool TryGetBrush(string key, out IBrush brush)
    {
        brush = Brushes.Transparent;
        if (Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var resource) != true)
        {
            return false;
        }

        if (resource is not IBrush foundBrush)
        {
            return false;
        }

        brush = foundBrush;
        return true;
    }
}
