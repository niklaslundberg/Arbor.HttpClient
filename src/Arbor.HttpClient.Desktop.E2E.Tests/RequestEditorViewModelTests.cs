using System.IO;
using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Core.Variables;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Focused unit tests for <see cref="RequestEditorViewModel"/>.
/// These tests do NOT require the Avalonia headless session — they exercise
/// the request-editor logic directly (URL/query-param sync, auth header building,
/// content-type resolution, request preview, BuildDraft).
/// </summary>
public class RequestEditorViewModelTests
{
    private static RequestEditorViewModel CreateEditor(
        IReadOnlyList<EnvironmentVariable>? variables = null,
        string? defaultContentType = null)
    {
        var variableList = variables ?? [];
        var editor = new RequestEditorViewModel(
            new VariableResolver(),
            () => variableList);

        if (defaultContentType is { } contentType)
        {
            editor.DefaultContentType = contentType;
        }

        return editor;
    }

    // ── URL ↔ query-parameter sync ──────────────────────────────────────────

    [Fact]
    public void QueryParameters_ArePopulated_WhenUrlContainsQueryString()
    {
        var editor = CreateEditor();
        editor.RequestUrl = "http://localhost:5000/search?q=hello&page=2";

        editor.RequestQueryParameters.Should().HaveCount(2);
        editor.RequestQueryParameters[0].Key.Should().Be("q");
        editor.RequestQueryParameters[0].Value.Should().Be("hello");
        editor.RequestQueryParameters[1].Key.Should().Be("page");
        editor.RequestQueryParameters[1].Value.Should().Be("2");
    }

    [Fact]
    public void QueryParameters_AreCleared_WhenUrlHasNoQuery()
    {
        var editor = CreateEditor();
        editor.RequestUrl = "http://localhost:5000/search?q=hello";
        editor.RequestUrl = "http://localhost:5000/search";

        editor.RequestQueryParameters.Should().BeEmpty();
    }

    [Fact]
    public void Url_IsUpdated_WhenQueryParameterValueChanges()
    {
        var editor = CreateEditor();
        editor.RequestUrl = "http://localhost:5000/items?foo=bar";

        editor.RequestQueryParameters[0].Value = "baz";

        editor.RequestUrl.Should().Contain("foo=baz");
        editor.RequestUrl.Should().NotContain("foo=bar");
    }

    [Fact]
    public void Url_IsUpdated_WhenDisabledQueryParameterIsExcluded()
    {
        var editor = CreateEditor();
        editor.RequestUrl = "http://localhost:5000/items?a=1&b=2";

        editor.RequestQueryParameters[0].IsEnabled = false;

        editor.RequestUrl.Should().NotContain("a=1");
        editor.RequestUrl.Should().Contain("b=2");
    }

    [Fact]
    public void AddQueryParameter_AddsEmptyEntryToCollection()
    {
        var editor = CreateEditor();
        editor.RequestUrl = "http://localhost:5000/items";
        editor.AddQueryParameterCommand.Execute(null);

        editor.RequestQueryParameters.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveQueryParameter_RemovesEntry()
    {
        var editor = CreateEditor();
        editor.RequestUrl = "http://localhost:5000/items?x=1";
        var param = editor.RequestQueryParameters[0];
        editor.RemoveQueryParameterCommand.Execute(param);

        editor.RequestQueryParameters.Should().BeEmpty();
    }

    // ── Request headers ─────────────────────────────────────────────────────

    [Fact]
    public void AddHeader_AddsEmptyHeader()
    {
        var editor = CreateEditor();
        editor.AddHeaderCommand.Execute(null);

        editor.RequestHeaders.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveHeader_RemovesHeader()
    {
        var editor = CreateEditor();
        editor.AddHeaderCommand.Execute(null);
        var header = editor.RequestHeaders[0];
        editor.RemoveHeaderCommand.Execute(header);

        editor.RequestHeaders.Should().BeEmpty();
    }

    // ── Auth header building ─────────────────────────────────────────────────

    [Theory]
    [InlineData(RequestEditorViewModel.AuthNoneOption, "Authorization: Bearer token")]
    public void RequestPreview_DoesNotContainAuthHeader_WhenAuthModeIsNone(string mode, string notExpected)
    {
        var editor = CreateEditor();
        editor.SelectedAuthModeOption = mode;
        editor.AuthBearerToken = "token";

        editor.RequestPreview.Should().NotContain(notExpected);
    }

    [Fact]
    public void RequestPreview_ContainsBearerToken_WhenBearerAuthModeSelected()
    {
        var editor = CreateEditor();
        editor.SelectedAuthModeOption = RequestEditorViewModel.AuthBearerOption;
        editor.AuthBearerToken = "my-token";

        editor.RequestPreview.Should().Contain("Authorization: Bearer my-token");
    }

    [Fact]
    public void RequestPreview_ContainsBasicCredentials_WhenBasicAuthModeSelected()
    {
        var editor = CreateEditor();
        editor.SelectedAuthModeOption = RequestEditorViewModel.AuthBasicOption;
        editor.AuthBasicUsername = "user";
        editor.AuthBasicPassword = "pass";

        // Base64 of "user:pass"
        var expected = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("user:pass"));
        editor.RequestPreview.Should().Contain($"Authorization: Basic {expected}");
    }

    [Fact]
    public void RequestPreview_BearerOverridesExistingAuthorizationHeader()
    {
        var editor = CreateEditor();
        editor.SelectedAuthModeOption = RequestEditorViewModel.AuthBearerOption;
        editor.AuthBearerToken = "new-token";
        editor.AddHeaderCommand.Execute(null);
        editor.RequestHeaders[0].Name = "Authorization";
        editor.RequestHeaders[0].Value = "Bearer old-token";
        editor.RequestHeaders[0].IsEnabled = true;

        editor.RequestPreview.Should().Contain("Authorization: Bearer new-token");
        editor.RequestPreview.Should().NotContain("Bearer old-token");
    }

    // ── Content-type resolution ──────────────────────────────────────────────

    [Fact]
    public void ContentType_IsEmpty_WhenNoneSelected()
    {
        var editor = CreateEditor();
        editor.SelectedContentTypeOption = RequestEditorViewModel.NoneContentTypeOption;

        editor.ContentType.Should().BeEmpty();
    }

    [Fact]
    public void ContentType_IsCustomValue_WhenCustomOptionSelectedAndValueProvided()
    {
        var editor = CreateEditor();
        editor.SelectedContentTypeOption = RequestEditorViewModel.CustomContentTypeOption;
        editor.CustomContentType = "application/vnd.api+json";

        editor.ContentType.Should().Be("application/vnd.api+json");
    }

    [Fact]
    public void RequestPreview_ContainsDefaultContentType_WhenBodyNonEmptyAndNoExplicitType()
    {
        var editor = CreateEditor(defaultContentType: "application/json");
        editor.SelectedContentTypeOption = RequestEditorViewModel.NoneContentTypeOption;
        editor.RequestBody = "{\"key\":\"value\"}";

        editor.RequestPreview.Should().Contain("Content-Type: application/json");
    }

    [Fact]
    public void RequestPreview_DoesNotContainContentType_WhenBodyIsEmpty()
    {
        var editor = CreateEditor(defaultContentType: "application/json");
        editor.RequestBody = string.Empty;

        editor.RequestPreview.Should().NotContain("Content-Type:");
    }

    // ── Variable resolution in preview ───────────────────────────────────────

    [Fact]
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

    [Fact]
    public void RequestPreview_ResolvesUnknownVariablesToEmpty_WhenVariablesNotDefined()
    {
        var editor = CreateEditor();
        editor.RequestUrl = "https://{{host}}/items";

        // VariableResolver replaces unresolved tokens with an empty string.
        editor.RequestPreview.Should().Contain("https:///items");
    }

    // ── BuildDraft ───────────────────────────────────────────────────────────

    [Fact]
    public void BuildDraft_ReturnsCorrectMethodAndUrl()
    {
        var editor = CreateEditor();
        editor.SelectedMethod = "POST";
        editor.RequestUrl = "http://localhost:5000/users";
        editor.RequestName = "Create user";

        var draft = editor.BuildDraft();

        draft.Method.Should().Be("POST");
        draft.Url.Should().Be("http://localhost:5000/users");
        draft.Name.Should().Be("Create user");
    }

    [Fact]
    public void BuildDraft_ResolvesVariables_InUrlAndBody()
    {
        var variables = new List<EnvironmentVariable>
        {
            new("base", "http://localhost:5000", IsEnabled: true)
        };
        var editor = CreateEditor(variables);
        editor.RequestUrl = "{{base}}/items";
        editor.RequestBody = "{\"url\":\"{{base}}\"}";

        var draft = editor.BuildDraft();

        draft.Url.Should().Be("http://localhost:5000/items");
        draft.Body.Should().Be("{\"url\":\"http://localhost:5000\"}");
    }

    [Fact]
    public void BuildDraft_UsesFollowRedirectsForRequest()
    {
        var editor = CreateEditor();
        editor.FollowRedirectsForRequest = false;

        var draft = editor.BuildDraft();

        draft.FollowRedirects.Should().BeFalse();
    }

    [Fact]
    public void BuildDraft_UsesPerRequestTimeoutSeconds_WhenProvided()
    {
        var editor = CreateEditor();
        editor.RequestTimeoutSecondsText = "12";

        var draft = editor.BuildDraft();

        draft.TimeoutSeconds.Should().Be(12);
    }

    [Fact]
    public void BuildDraft_UsesNullTimeout_WhenPerRequestTimeoutIsBlank()
    {
        var editor = CreateEditor();
        editor.RequestTimeoutSecondsText = " ";

        var draft = editor.BuildDraft();

        draft.TimeoutSeconds.Should().BeNull();
    }

    [Fact]
    public void BuildDraft_ThrowsInvalidDataException_WhenPerRequestTimeoutIsInvalid()
    {
        var editor = CreateEditor();
        editor.RequestTimeoutSecondsText = "abc";

        var action = () => editor.BuildDraft();

        action.Should().Throw<InvalidDataException>().WithMessage("*positive whole number*");
    }

    [Fact]
    public void BuildDraft_UsesSelectedHttpVersion()
    {
        var editor = CreateEditor();
        editor.SelectedHttpVersionOption = "2.0";

        var draft = editor.BuildDraft();

        draft.HttpVersion.Should().Be(global::System.Net.HttpVersion.Version20);
    }

    [Fact]
    public void BuildDraft_IncludesSelectedContentTypeInHeaders_WhenBodyProvided()
    {
        var editor = CreateEditor();
        editor.SelectedContentTypeOption = "application/xml";
        editor.SelectedMethod = "POST";
        editor.RequestBody = "<root/>";

        var draft = editor.BuildDraft();

        draft.Headers.Should().ContainSingle(h =>
            string.Equals(h.Name, "Content-Type", StringComparison.OrdinalIgnoreCase) &&
            h.Value == "application/xml");
    }

    [Fact]
    public void BuildDraft_DoesNotDuplicateContentTypeHeader_WhenUserAlsoAddsOneManually()
    {
        var editor = CreateEditor();
        editor.SelectedContentTypeOption = "application/xml";
        editor.RequestBody = "<root/>";
        editor.AddHeaderCommand.Execute(null);
        editor.RequestHeaders[0].Name = "Content-Type";
        editor.RequestHeaders[0].Value = "text/plain";
        editor.RequestHeaders[0].IsEnabled = true;

        var draft = editor.BuildDraft();

        draft.Headers.Should().ContainSingle(h =>
            string.Equals(h.Name, "Content-Type", StringComparison.OrdinalIgnoreCase));
        var contentTypeHeader = draft.Headers!.First(h => string.Equals(h.Name, "Content-Type", StringComparison.OrdinalIgnoreCase));
        contentTypeHeader.Value.Should().Be("application/xml");
    }

    // ── HTTP version ─────────────────────────────────────────────────────────

    [Fact]
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

    [Fact]
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

    // ── GetResolvedVariables ─────────────────────────────────────────────────

    [Fact]
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

    [Fact]
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
}
