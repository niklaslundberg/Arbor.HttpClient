using System;
using System.Globalization;
using System.Reactive.Linq;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Desktop.Shared;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Arbor.HttpClient.Desktop.Features.Environments;

public sealed partial class EnvironmentVariableViewModel : ReactiveViewModelBase
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

    [Reactive]
    private bool _isEnabled;

    [Reactive]
    private string _name;

    [Reactive]
    private string _value;

    [Reactive]
    private bool _isSensitive;

    [Reactive]
    private DateTimeOffset? _expiresAtUtc;

    /// <summary>When <c>true</c> the actual (unredacted) value is shown in the UI.</summary>
    [Reactive]
    private bool _isValueRevealed;

    private readonly ObservableAsPropertyHelper<bool> _isValueMasked;
    private readonly ObservableAsPropertyHelper<bool> _isExpired;

    /// <summary>Returns <c>true</c> when the value should be displayed as masked bullets (sensitive and not revealed).</summary>
    public bool IsValueMasked => _isValueMasked.Value;

    /// <summary>Returns <c>true</c> when the variable has expired.</summary>
    public bool IsExpired => _isExpired.Value;

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
            this.RaisePropertyChanged(nameof(ExpiresAtUtcText));
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
        // Mark as user-overridden when the variable already has a name (i.e. was loaded
        // from storage), so that a later name edit cannot silently re-enable sensitivity
        // regardless of whether the stored value was true or false.
        // For truly new variables (name is empty) the override stays false so that
        // auto-detection fires as soon as the user types the name.
        _sensitiveUserOverride = !string.IsNullOrEmpty(name) || isSensitive;
        _expiresAtUtc = expiresAtUtc;
        // If an expiry was loaded from storage, treat it as a user-set value so JWT
        // auto-detection does not silently overwrite it.
        _expiryUserOverride = expiresAtUtc.HasValue;

        _isValueMasked = this
            .WhenAnyValue(
                viewModel => viewModel.IsSensitive,
                viewModel => viewModel.IsValueRevealed,
                (sensitive, revealed) => sensitive && !revealed)
            .ToProperty(this, viewModel => viewModel.IsValueMasked);

        _isExpired = this
            .WhenAnyValue(
                viewModel => viewModel.ExpiresAtUtc,
                expires => expires.HasValue && DateTimeOffset.UtcNow >= expires.Value)
            .ToProperty(this, viewModel => viewModel.IsExpired);

        // Auto-detect sensitivity only when the user has never manually set the checkbox.
        this.WhenAnyValue(viewModel => viewModel.Name)
            .Skip(1)
            .Where(currentName => !_sensitiveUserOverride && SensitiveVariableDetector.IsSensitive(currentName))
            .Subscribe(_ => IsSensitive = true);

        // Auto-detect JWT expiry from the value when the user has not manually set an expiry.
        this.WhenAnyValue(viewModel => viewModel.Value)
            .Skip(1)
            .Subscribe(currentValue =>
            {
                if (!_expiryUserOverride && JwtExpiryExtractor.TryGetExpiry(currentValue, out var jwtExpiry))
                {
                    ExpiresAtUtc = jwtExpiry;
                }
            });

        // Treat any change via the checkbox as a deliberate user override.
        // When sensitivity is toggled off, hide the value again.
        this.WhenAnyValue(viewModel => viewModel.IsSensitive)
            .Skip(1)
            .Subscribe(sensitive =>
            {
                _sensitiveUserOverride = true;
                if (!sensitive)
                {
                    IsValueRevealed = false;
                }
            });

        this.WhenAnyValue(viewModel => viewModel.ExpiresAtUtc)
            .Skip(1)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(ExpiresAtUtcText)));
    }

    [ReactiveCommand]
    private void ToggleReveal() => IsValueRevealed = !IsValueRevealed;
}
