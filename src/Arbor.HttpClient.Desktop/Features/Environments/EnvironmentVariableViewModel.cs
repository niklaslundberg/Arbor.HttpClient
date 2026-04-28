using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Desktop.Shared;

namespace Arbor.HttpClient.Desktop.Features.Environments;

public sealed partial class EnvironmentVariableViewModel : ViewModelBase
{
    /// <summary>
    /// Set to <c>true</c> when the user has manually toggled the Sensitive checkbox,
    /// preventing auto-detection from overriding the explicit choice.
    /// </summary>
    private bool _sensitiveUserOverride;

    /// <summary>
    /// Set to <c>true</c> when the user has manually set (or cleared) the expiry,
    /// preventing JWT auto-detection from overriding the explicit value.
    /// </summary>
    private bool _expiryUserOverride;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _value;

    [ObservableProperty]
    private bool _isSensitive;

    [ObservableProperty]
    private DateTimeOffset? _expiresAtUtc;

    /// <summary>When <c>true</c> the actual (unredacted) value is shown in the UI.</summary>
    [ObservableProperty]
    private bool _isValueRevealed;

    /// <summary>Returns <c>true</c> when the value should be displayed as masked bullets (sensitive and not revealed).</summary>
    public bool IsValueMasked => IsSensitive && !IsValueRevealed;

    /// <summary>Returns a redacted placeholder when the variable is sensitive and not revealed.</summary>
    public string DisplayValue => IsSensitive && !IsValueRevealed ? "••••••••" : Value;

    /// <summary>Returns <c>true</c> when the variable has expired.</summary>
    public bool IsExpired => ExpiresAtUtc.HasValue && DateTimeOffset.UtcNow >= ExpiresAtUtc.Value;

    /// <summary>
    /// Text representation of <see cref="ExpiresAtUtc"/> for two-way binding to a TextBox.
    /// Accepts ISO 8601 date/time strings with a UTC offset (e.g. <c>2026-12-31T23:59:00Z</c>).
    /// Setting an empty or whitespace string clears the expiry.
    /// Invalid input is silently ignored and the existing value is retained.
    /// </summary>
    public string ExpiresAtUtcText
    {
        get => ExpiresAtUtc?.ToUniversalTime().ToString("O") ?? string.Empty;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                // Explicit clear — user is removing the expiry.
                _expiryUserOverride = true;
                ExpiresAtUtc = null;
            }
            else if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                // Normalise to UTC regardless of the input offset.
                _expiryUserOverride = true;
                ExpiresAtUtc = parsed.ToUniversalTime();
            }
            // Invalid input is silently ignored; the existing value is retained.
            OnPropertyChanged();
        }
    }

    public EnvironmentVariableViewModel(
        string name,
        string value,
        bool isEnabled = true,
        bool isSensitive = false,
        DateTimeOffset? expiresAtUtc = null)
    {
        _isEnabled = isEnabled;
        _name = name;
        _value = value;
        _isSensitive = isSensitive;
        // Mark as user-overridden when loaded from storage so that typing the name
        // does not reset an explicitly stored sensitive flag.
        _sensitiveUserOverride = isSensitive;
        _expiresAtUtc = expiresAtUtc;
        // If an expiry was loaded from storage, treat it as a user-set value so JWT
        // auto-detection does not silently overwrite it.
        _expiryUserOverride = expiresAtUtc.HasValue;
    }

    partial void OnNameChanged(string value)
    {
        // Auto-detect sensitivity only when the user has never manually set the checkbox.
        if (!_sensitiveUserOverride && SensitiveVariableDetector.IsSensitive(value))
        {
            IsSensitive = true;
        }
    }

    partial void OnExpiresAtUtcChanged(DateTimeOffset? value)
    {
        OnPropertyChanged(nameof(ExpiresAtUtcText));
        OnPropertyChanged(nameof(IsExpired));
    }

    partial void OnIsSensitiveChanged(bool value)
    {
        // Treat any change via the checkbox as a deliberate user override.
        _sensitiveUserOverride = true;
        // When sensitivity is toggled off, hide the value again.
        if (!value)
        {
            IsValueRevealed = false;
        }
        OnPropertyChanged(nameof(DisplayValue));
        OnPropertyChanged(nameof(IsValueMasked));
    }

    partial void OnValueChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayValue));

        // Auto-detect JWT expiry from the value when the user has not manually set an expiry.
        if (!_expiryUserOverride && JwtExpiryExtractor.TryGetExpiry(value, out var jwtExpiry))
        {
            ExpiresAtUtc = jwtExpiry;
        }
    }

    partial void OnIsValueRevealedChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayValue));
        OnPropertyChanged(nameof(IsValueMasked));
    }

    [RelayCommand]
    private void ToggleReveal() => IsValueRevealed = !IsValueRevealed;
}
