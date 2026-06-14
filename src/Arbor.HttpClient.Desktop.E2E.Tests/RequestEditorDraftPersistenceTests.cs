using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Core.Variables;
using static Arbor.HttpClient.Desktop.E2E.Tests.RequestEditorTestHelpers;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Focused unit tests for <see cref="RequestEditorViewModel"/>: resolved HTTP request draft building (variables, timeouts, TLS, HTTP version) and bulk-update suppression.
/// These tests do NOT require the Avalonia headless session.
/// </summary>
public class RequestEditorDraftPersistenceTests
{
    [AvaloniaFact(Timeout = 10_000)]
    public void RequestPreview_ResolvesVariables_UsingActiveEnvironment()
    {
        var variables = new List<EnvironmentVariable>
        {
            new("host", "localhost:5000", IsEnabled: true),
            new("token", "abc123", IsEnabled: true)
        };
        var editor = CreateEditor(variables);
        editor.RequestUrl = "http://{{host}}/items";
        editor.SelectedAuthModeOption = RequestEditorViewModel.AuthBearerOption;
        editor.AuthBearerToken = "{{token}}";

        editor.RequestPreview.Should().Contain("http://localhost:5000/items");
        editor.RequestPreview.Should().Contain("Authorization: Bearer abc123");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void RequestPreview_ResolvesUnknownVariablesToEmpty_WhenVariablesNotDefined()
    {
        var editor = CreateEditor();
        editor.RequestUrl = "https://{{host}}/items";

        // VariableResolver replaces unresolved tokens with an empty string.
        editor.RequestPreview.Should().Contain("https:///items");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void BuildResolvedHttpRequestDraft_ReturnsCorrectMethodAndUrl()
    {
        var editor = CreateEditor();
        editor.SelectedMethod = "POST";
        editor.RequestUrl = "http://localhost:5000/users";
        editor.RequestName = "Create user";

        var draft = editor.BuildResolvedHttpRequestDraft();

        draft.Method.Should().Be("POST");
        draft.Url.Should().Be("http://localhost:5000/users");
        draft.Name.Should().Be("Create user");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void BuildResolvedHttpRequestDraft_ResolvesVariables_InUrlAndBody()
    {
        var variables = new List<EnvironmentVariable>
        {
            new("base", "http://localhost:5000", IsEnabled: true)
        };
        var editor = CreateEditor(variables);
        editor.RequestUrl = "{{base}}/items";
        editor.RequestBody = "{\"url\":\"{{base}}\"}";

        var draft = editor.BuildResolvedHttpRequestDraft();

        draft.Url.Should().Be("http://localhost:5000/items");
        draft.Body.Should().Be("{\"url\":\"http://localhost:5000\"}");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void BuildResolvedHttpRequestDraft_UsesFollowRedirectsForRequest()
    {
        var editor = CreateEditor();
        editor.FollowRedirectsForRequest = false;

        var draft = editor.BuildResolvedHttpRequestDraft();

        draft.FollowRedirects.Should().BeFalse();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void ValidateUrlBeforeSend_IsEnabledByDefault()
    {
        var editor = CreateEditor();

        editor.ValidateUrlBeforeSend.Should().BeTrue();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void BuildResolvedHttpRequestDraft_SetsIgnoreCertificateValidation_WhenEnabled()
    {
        var editor = CreateEditor();
        editor.IgnoreCertificateValidationForRequest = true;

        var draft = editor.BuildResolvedHttpRequestDraft();

        draft.IgnoreCertificateValidation.Should().BeTrue();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void BuildResolvedHttpRequestDraft_LeavesIgnoreCertificateValidationNull_WhenDisabled()
    {
        var editor = CreateEditor();
        editor.IgnoreCertificateValidationForRequest = false;

        var draft = editor.BuildResolvedHttpRequestDraft();

        draft.IgnoreCertificateValidation.Should().BeNull();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void BuildResolvedHttpRequestDraft_UsesNullTlsVersionOverride_WhenDefaultOptionSelected()
    {
        var editor = CreateEditor();
        editor.SelectedTlsVersionOverrideOption = RequestEditorViewModel.DefaultTlsVersionOverrideOption;

        var draft = editor.BuildResolvedHttpRequestDraft();

        draft.TlsVersionOverride.Should().BeNull();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void BuildResolvedHttpRequestDraft_UsesPerRequestTlsVersionOverride_WhenSpecified()
    {
        var editor = CreateEditor();
        editor.SelectedTlsVersionOverrideOption = "Tls13";

        var draft = editor.BuildResolvedHttpRequestDraft();

        draft.TlsVersionOverride.Should().Be("Tls13");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void BuildResolvedHttpRequestDraft_UsesPerRequestTimeoutSeconds_WhenProvided()
    {
        var editor = CreateEditor();
        editor.RequestTimeoutSecondsText = "12";

        var draft = editor.BuildResolvedHttpRequestDraft();

        draft.TimeoutSeconds.Should().Be(12);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void BuildResolvedHttpRequestDraft_UsesNoTimeout_WhenPerRequestTimeoutIsZero()
    {
        var editor = CreateEditor();
        editor.RequestTimeoutSecondsText = "0";

        var draft = editor.BuildResolvedHttpRequestDraft();

        draft.TimeoutSeconds.Should().Be(0);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void BuildResolvedHttpRequestDraft_UsesNullTimeout_WhenPerRequestTimeoutIsBlank()
    {
        var editor = CreateEditor();
        editor.RequestTimeoutSecondsText = " ";

        var draft = editor.BuildResolvedHttpRequestDraft();

        draft.TimeoutSeconds.Should().BeNull();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void BuildResolvedHttpRequestDraft_UsesNullTimeout_WhenPerRequestTimeoutContainsNoDigits()
    {
        var editor = CreateEditor();
        editor.RequestTimeoutSecondsText = "abc";
        var draft = editor.BuildResolvedHttpRequestDraft();
        draft.TimeoutSeconds.Should().BeNull();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void BuildResolvedHttpRequestDraft_ClampsPerRequestTimeoutToMaximum()
    {
        var editor = CreateEditor();
        editor.RequestTimeoutSecondsText = "101";
        var draft = editor.BuildResolvedHttpRequestDraft();
        draft.TimeoutSeconds.Should().Be(100);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void RequestTimeoutSecondsText_ShouldClear_WhenInputContainsNonDigits()
    {
        var editor = CreateEditor();
        editor.RequestTimeoutSecondsText = "a1b2c3";

        editor.RequestTimeoutSecondsText.Should().BeEmpty();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void RequestTimeoutSecondsText_ShouldClampToMaximum_WhenInputIsNumeric()
    {
        var editor = CreateEditor();
        editor.RequestTimeoutSecondsText = "123";

        editor.RequestTimeoutSecondsText.Should().Be("100");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void BuildResolvedHttpRequestDraft_UsesSelectedHttpVersion()
    {
        var editor = CreateEditor();
        editor.SelectedHttpVersionOption = "2.0";

        var draft = editor.BuildResolvedHttpRequestDraft();

        draft.HttpVersion.Should().Be(System.Net.HttpVersion.Version20);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void OptionsAffectingPropertyChanged_IsFired_WhenHttpVersionChanges()
    {
        var fired = false;
        var editor = new RequestEditorViewModel(
            new VariableResolver(),
            () => [],
            onOptionsAffectingPropertyChanged: () => fired = true);

        editor.SelectedHttpVersionOption = "2.0";

        fired.Should().BeTrue();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void OptionsAffectingPropertyChanged_IsNotFired_WhenHttpVersionSetToSameValue()
    {
        var count = 0;
        var editor = new RequestEditorViewModel(
            new VariableResolver(),
            () => [],
            onOptionsAffectingPropertyChanged: () => count++);

        editor.SelectedHttpVersionOption = "1.1"; // same as default

        count.Should().Be(0);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void GetResolvedVariables_FiltersOutDisabledVariables()
    {
        var variables = new List<EnvironmentVariable>
        {
            new("a", "1", IsEnabled: true),
            new("b", "2", IsEnabled: false)
        };
        var editor = CreateEditor(variables);

        var resolved = editor.GetResolvedVariables();

        resolved.Should().HaveCount(1);
        resolved[0].Name.Should().Be("a");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void GetResolvedVariables_FiltersOutVariablesWithEmptyName()
    {
        var variables = new List<EnvironmentVariable>
        {
            new("", "value", IsEnabled: true),
            new("   ", "other", IsEnabled: true)
        };
        var editor = CreateEditor(variables);

        var resolved = editor.GetResolvedVariables();

        resolved.Should().BeEmpty();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void BeginBulkUpdate_SuppressesRefreshDuringScope_ThenFiresOneRefreshOnDispose()
    {
        var editor = CreateEditor();
        editor.SelectedContentTypeOption = "application/json";
        editor.RequestUrl = "http://localhost/test";

        var previewChangedCount = 0;
        editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RequestEditorViewModel.RequestPreview))
            {
                previewChangedCount++;
            }
        };

        // Set multiple properties inside the suppression window.
        using (editor.BeginBulkUpdate())
        {
            editor.SelectedMethod = "POST";
            editor.RequestBody = "{}";
            editor.RequestUrl = "http://localhost/bulk";
            // No refresh should have fired yet.
            previewChangedCount.Should().Be(0);
        }

        // Exactly one refresh should have fired when the handle was disposed.
        previewChangedCount.Should().Be(1);
        editor.RequestPreview.Should().Contain("POST");
        editor.RequestPreview.Should().Contain("/bulk");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void EndBulkUpdate_AfterBeginBulkUpdate_FiresOneRefresh()
    {
        var editor = CreateEditor();
        editor.RequestUrl = "http://localhost/before";

        var previewChangedCount = 0;
        editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RequestEditorViewModel.RequestPreview))
            {
                previewChangedCount++;
            }
        };

        editor.BeginBulkUpdate();
        editor.SelectedMethod = "PUT";
        editor.RequestUrl = "http://localhost/after";
        previewChangedCount.Should().Be(0);

        editor.EndBulkUpdate();

        previewChangedCount.Should().Be(1);
        editor.RequestPreview.Should().Contain("PUT");
        editor.RequestPreview.Should().Contain("/after");
    }
}
