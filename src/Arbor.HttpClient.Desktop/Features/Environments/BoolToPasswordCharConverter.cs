using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Arbor.HttpClient.Desktop.Features.Environments;

/// <summary>
/// Converts a boolean to a password masking character.
/// <c>true</c> (masked) → '•'; <c>false</c> (revealed) → '\0' (no masking).
/// Used to toggle the Avalonia <c>TextBox.PasswordChar</c> property.
/// </summary>
public sealed class BoolToPasswordCharConverter : IValueConverter
{
    public static readonly BoolToPasswordCharConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? '•' : '\0';

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
