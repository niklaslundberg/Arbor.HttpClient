using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Arbor.HttpClient.Desktop.Converters;

public sealed class MethodToColorConverter : IValueConverter
{
    public static readonly MethodToColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var method = value as string;

        var brushKey = method switch
        {
            "GET" => "MethodGetBrush",
            "POST" => "MethodPostBrush",
            "PUT" => "MethodPutBrush",
            "PATCH" => "MethodPatchBrush",
            "DELETE" => "MethodDeleteBrush",
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
