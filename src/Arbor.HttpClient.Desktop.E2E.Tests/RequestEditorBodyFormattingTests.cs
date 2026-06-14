using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Core.Variables;
using static Arbor.HttpClient.Desktop.E2E.Tests.RequestEditorTestHelpers;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Focused unit tests for <see cref="RequestEditorViewModel"/>: content-type resolution and request-body pretty-printing.
/// These tests do NOT require the Avalonia headless session.
/// </summary>
public class RequestEditorBodyFormattingTests
{
    [AvaloniaFact(Timeout = 10_000)]
    public void ContentType_IsEmpty_WhenNoneSelected()
    {
        var editor = CreateEditor();
        editor.SelectedContentTypeOption = RequestEditorViewModel.NoneContentTypeOption;

        editor.ContentType.Should().BeEmpty();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void ContentType_IsCustomValue_WhenCustomOptionSelectedAndValueProvided()
    {
        var editor = CreateEditor();
        editor.SelectedContentTypeOption = RequestEditorViewModel.CustomContentTypeOption;
        editor.CustomContentType = "application/vnd.api+json";

        editor.ContentType.Should().Be("application/vnd.api+json");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void RequestPreview_ContainsDefaultContentType_WhenBodyNonEmptyAndNoExplicitType()
    {
        var editor = CreateEditor(defaultContentType: "application/json");
        editor.SelectedContentTypeOption = RequestEditorViewModel.NoneContentTypeOption;
        editor.RequestBody = "{\"key\":\"value\"}";

        editor.RequestPreview.Should().Contain("Content-Type: application/json");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void RequestPreview_DoesNotContainContentType_WhenBodyIsEmpty()
    {
        var editor = CreateEditor(defaultContentType: "application/json");
        editor.RequestBody = string.Empty;

        editor.RequestPreview.Should().NotContain("Content-Type:");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void RequestPreview_PrettyPrintsResolvedBody_WhenEnabled()
    {
        var editor = CreateEditor();
        editor.SelectedContentTypeOption = "application/json";
        editor.RequestBody = "{\"a\":1}";
        editor.PrettyPrintRequestBody = true;
        editor.PrettyPrintRequestBodyUseIndentation = true;

        editor.RequestPreview.Should().Contain("{\n  \"a\": 1\n}");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void ShowRequestPreview_IsEnabledByDefault()
    {
        var editor = CreateEditor();

        editor.ShowRequestPreview.Should().BeTrue();
        editor.IsRequestPreviewPanelVisible.Should().BeTrue();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void BuildResolvedHttpRequestDraft_PrettyPrintsJsonBodyWithIndentation_WhenEnabled()
    {
        var editor = CreateEditor();
        editor.SelectedContentTypeOption = "application/json";
        editor.RequestBody = "{\"a\":1}";
        editor.PrettyPrintRequestBody = true;
        editor.PrettyPrintRequestBodyUseIndentation = true;

        var draft = editor.BuildResolvedHttpRequestDraft();

        draft.Body.Should().Be("{\n  \"a\": 1\n}");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void BuildResolvedHttpRequestDraft_PrettyPrintsJsonBodyWithoutIndentation_WhenEnabled()
    {
        var editor = CreateEditor();
        editor.SelectedContentTypeOption = "application/json";
        editor.RequestBody = "{  \"a\" : 1  }";
        editor.PrettyPrintRequestBody = true;
        editor.PrettyPrintRequestBodyUseIndentation = false;

        var draft = editor.BuildResolvedHttpRequestDraft();

        draft.Body.Should().Be("{\"a\":1}");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void BuildResolvedHttpRequestDraft_PrettyPrintJson_DoesNotEscapeAngleBracketsOrAmpersands()
    {
        var editor = CreateEditor();
        editor.SelectedContentTypeOption = "application/json";
        editor.RequestBody = "{\"html\":\"<div>&</div>\"}";
        editor.PrettyPrintRequestBody = true;
        editor.PrettyPrintRequestBodyUseIndentation = false;

        var draft = editor.BuildResolvedHttpRequestDraft();

        draft.Body.Should().Be("{\"html\":\"<div>&</div>\"}");
        draft.Body.Should().NotContain("\\u003c");
        draft.Body.Should().NotContain("\\u003e");
        draft.Body.Should().NotContain("\\u0026");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void BuildResolvedHttpRequestDraft_PrettyPrintsXmlBodyWithoutIndentation_WhenEnabled()
    {
        var editor = CreateEditor();
        editor.SelectedContentTypeOption = "application/xml";
        editor.RequestBody = "<root>\n  <item>1</item>\n</root>";
        editor.PrettyPrintRequestBody = true;
        editor.PrettyPrintRequestBodyUseIndentation = false;

        var draft = editor.BuildResolvedHttpRequestDraft();

        draft.Body.Should().Be("<root><item>1</item></root>");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void PrettyPrintRequestBodySourceCommand_FormatsSourceBody()
    {
        var editor = CreateEditor();
        editor.SelectedContentTypeOption = "application/json";
        editor.PrettyPrintRequestBodyUseIndentation = false;
        editor.RequestBody = "{ \"a\" : 1 }";

        editor.PrettyPrintRequestBodySourceCommand.Execute().Subscribe();

        editor.RequestBody.Should().Be("{\"a\":1}");
    }
}
