using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Arbor.HttpClient.Desktop.Features.Environments;

/// <summary>
/// Converts the <c>IsValueRevealed</c> boolean to a reveal-toggle icon.
/// <c>true</c> (currently revealed) → "🔒" (click to hide);
/// <c>false</c> (currently masked) → "👁" (click to reveal).
/// </summary>
public sealed class BoolToRevealIconConverter : IValueConverter
{
    public static readonly BoolToRevealIconConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "🔒" : "👁";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
