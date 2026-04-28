using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Desktop.Shared;

namespace Arbor.HttpClient.Desktop.Features.Environments;

public sealed partial class EnvironmentVariableViewModel : ViewModelBase
{
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
    /// Accepts ISO 8601 date/time strings (e.g. <c>2026-12-31T23:59:00Z</c> or <c>2026-12-31</c>).
    /// Setting an empty or whitespace string clears the expiry.
    /// </summary>
    public string ExpiresAtUtcText
    {
        get => ExpiresAtUtc?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? string.Empty;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                ExpiresAtUtc = null;
            }
            else if (DateTimeOffset.TryParse(value, null, DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal, out var parsed))
            {
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
        _expiresAtUtc = expiresAtUtc;
    }

    partial void OnNameChanged(string value)
    {
        // Auto-detect sensitivity when the name is changed, but only if the user
        // has not explicitly flagged it yet.
        if (SensitiveVariableDetector.IsSensitive(value) && !_isSensitive)
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
    }

    partial void OnIsValueRevealedChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayValue));
        OnPropertyChanged(nameof(IsValueMasked));
    }

    [RelayCommand]
    private void ToggleReveal() => IsValueRevealed = !IsValueRevealed;
}
