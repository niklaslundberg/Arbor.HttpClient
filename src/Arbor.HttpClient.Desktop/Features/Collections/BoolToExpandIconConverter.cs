using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Arbor.HttpClient.Desktop.Features.Collections;

/// <summary>
/// Converts a boolean to a tree-expand icon: true → "▼" (expanded), false → "▶" (collapsed).
/// </summary>
public sealed class BoolToExpandIconConverter : IValueConverter
{
    public static readonly BoolToExpandIconConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "▼" : "▶";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
