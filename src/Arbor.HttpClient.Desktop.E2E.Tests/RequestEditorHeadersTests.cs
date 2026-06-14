using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Core.Variables;
using static Arbor.HttpClient.Desktop.E2E.Tests.RequestEditorTestHelpers;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Focused unit tests for <see cref="RequestEditorViewModel"/>: request and auth headers, including content-type headers.
/// These tests do NOT require the Avalonia headless session.
/// </summary>
public class RequestEditorHeadersTests
{
    [AvaloniaFact(Timeout = 10_000)]
    public void AddHeader_AddsEmptyHeader()
    {
        var editor = CreateEditor();
        editor.AddHeaderCommand.Execute().Subscribe();

        editor.RequestHeaders.Should().HaveCount(1);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void RemoveHeader_RemovesHeader()
    {
        var editor = CreateEditor();
        editor.AddHeaderCommand.Execute().Subscribe();
        var header = editor.RequestHeaders[0];
        editor.RemoveHeaderCommand.Execute(header).Subscribe();

        editor.RequestHeaders.Should().HaveCount(1);
        editor.RequestHeaders[0].Name.Should().BeEmpty();
        editor.RequestHeaders[0].IsEnabled.Should().BeFalse();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void PlaceholderHeader_AutoEnablesAndAppendsNewPlaceholder_WhenNameIsTyped()
    {
        var editor = CreateEditor();
        var placeholder = editor.RequestHeaders[0];
        placeholder.Name.Should().BeEmpty();
        placeholder.IsEnabled.Should().BeFalse();

        placeholder.Name = "X-Custom";

        placeholder.IsEnabled.Should().BeTrue();
        editor.RequestHeaders.Should().HaveCount(2);
        editor.RequestHeaders[^1].Name.Should().BeEmpty();
        editor.RequestHeaders[^1].IsEnabled.Should().BeFalse();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void PlaceholderHeader_CannotBeEnabled_WhenNameIsBlank()
    {
        var editor = CreateEditor();
        var placeholder = editor.RequestHeaders[0];

        placeholder.IsEnabled = true;

        editor.RequestHeaders.Should().HaveCount(1);
        editor.RequestHeaders[0].Name.Should().BeEmpty();
        editor.RequestHeaders[0].IsEnabled.Should().BeFalse();
    }

    [AvaloniaTheory(Timeout = 10_000)]
    [InlineData(RequestEditorViewModel.AuthNoneOption, "Authorization: Bearer token")]
    public void RequestPreview_DoesNotContainAuthHeader_WhenAuthModeIsNone(string mode, string notExpected)
    {
        var editor = CreateEditor();
        editor.SelectedAuthModeOption = mode;
        editor.AuthBearerToken = "token";

        editor.RequestPreview.Should().NotContain(notExpected);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void RequestPreview_ContainsBearerToken_WhenBearerAuthModeSelected()
    {
        var editor = CreateEditor();
        editor.SelectedAuthModeOption = RequestEditorViewModel.AuthBearerOption;
        editor.AuthBearerToken = "my-token";

        editor.RequestPreview.Should().Contain("Authorization: Bearer my-token");
    }

    [AvaloniaFact(Timeout = 10_000)]
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

    [AvaloniaFact(Timeout = 10_000)]
    public void RequestPreview_BearerOverridesExistingAuthorizationHeader()
    {
        var editor = CreateEditor();
        editor.SelectedAuthModeOption = RequestEditorViewModel.AuthBearerOption;
        editor.AuthBearerToken = "new-token";
        editor.AddHeaderCommand.Execute().Subscribe();
        editor.RequestHeaders[0].Name = "Authorization";
        editor.RequestHeaders[0].Value = "Bearer old-token";
        editor.RequestHeaders[0].IsEnabled = true;

        editor.RequestPreview.Should().Contain("Authorization: Bearer new-token");
        editor.RequestPreview.Should().NotContain("Bearer old-token");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void BuildResolvedHttpRequestDraft_IncludesSelectedContentTypeInHeaders_WhenBodyProvided()
    {
        var editor = CreateEditor();
        editor.SelectedContentTypeOption = "application/xml";
        editor.SelectedMethod = "POST";
        editor.RequestBody = "<root/>";

        var draft = editor.BuildResolvedHttpRequestDraft();

        draft.Headers.Should().ContainSingle(h =>
            string.Equals(h.Name, "Content-Type", StringComparison.OrdinalIgnoreCase) &&
            h.Value == "application/xml");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void BuildResolvedHttpRequestDraft_DoesNotDuplicateContentTypeHeader_WhenUserAlsoAddsOneManually()
    {
        var editor = CreateEditor();
        editor.SelectedContentTypeOption = "application/xml";
        editor.RequestBody = "<root/>";
        editor.AddHeaderCommand.Execute().Subscribe();
        editor.RequestHeaders[0].Name = "Content-Type";
        editor.RequestHeaders[0].Value = "text/plain";
        editor.RequestHeaders[0].IsEnabled = true;

        var draft = editor.BuildResolvedHttpRequestDraft();

        draft.Headers.Should().ContainSingle(h =>
            string.Equals(h.Name, "Content-Type", StringComparison.OrdinalIgnoreCase));
        var contentTypeHeader = draft.Headers!.First(h => string.Equals(h.Name, "Content-Type", StringComparison.OrdinalIgnoreCase));
        contentTypeHeader.Value.Should().Be("application/xml");
    }
}
