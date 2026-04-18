using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Arbor.HttpClient.Desktop.Converters;

/// <summary>Returns true when the string value equals the converter parameter.</summary>
public sealed class StringEqualityConverter : IValueConverter
{
    public static readonly StringEqualityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value as string, parameter as string, StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
