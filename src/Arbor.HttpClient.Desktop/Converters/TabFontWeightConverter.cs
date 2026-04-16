using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Arbor.HttpClient.Desktop.Converters;

/// <summary>Returns Bold when the bound tab name equals the parameter, else Normal.</summary>
public sealed class TabFontWeightConverter : IValueConverter
{
    public static readonly TabFontWeightConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value as string, parameter as string, StringComparison.Ordinal)
            ? FontWeight.Bold
            : FontWeight.Normal;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
