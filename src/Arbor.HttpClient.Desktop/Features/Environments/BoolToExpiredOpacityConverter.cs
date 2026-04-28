using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Arbor.HttpClient.Desktop.Features.Environments;

/// <summary>
/// Converts the <c>IsExpired</c> boolean to an opacity value.
/// <c>true</c> (expired) → 0.4 (dimmed); <c>false</c> → 1.0 (normal).
/// </summary>
public sealed class BoolToExpiredOpacityConverter : IValueConverter
{
    public static readonly BoolToExpiredOpacityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 0.4 : 1.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
