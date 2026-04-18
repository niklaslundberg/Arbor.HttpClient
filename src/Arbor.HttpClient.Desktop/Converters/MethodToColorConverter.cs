using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Arbor.HttpClient.Desktop.Converters;

public sealed class MethodToColorConverter : IValueConverter
{
    public static readonly MethodToColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var method = value as string;

        IBrush brush = method switch
        {
            "GET" => new SolidColorBrush(Color.FromRgb(0x61, 0xAF, 0xEF)),    // blue
            "POST" => new SolidColorBrush(Color.FromRgb(0x98, 0xC3, 0x79)),   // green
            "PUT" => new SolidColorBrush(Color.FromRgb(0xE5, 0xC0, 0x7B)),    // yellow/amber
            "PATCH" => new SolidColorBrush(Color.FromRgb(0xC6, 0x78, 0xDD)), // purple
            "DELETE" => new SolidColorBrush(Color.FromRgb(0xE0, 0x6C, 0x75)), // red
            _ => Brushes.White
        };

        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
