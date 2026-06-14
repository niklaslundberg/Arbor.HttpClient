using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Focused unit tests for <see cref="ResponseActionsViewModel"/>.
/// These tests exercise the pure logic (static helpers, TryGetSaveableResponseContent)
/// directly via <see cref="StubResponseActionsContext"/> without requiring the full
/// <c>MainWindowViewModel</c>. Clipboard-dependent behaviour is covered by the
/// integration tests in <see cref="ResponseShortcutsTests"/> which run via the headless
/// Avalonia session and the real clipboard.
/// </summary>
public class ResponseActionsViewModelTests
{
    // ── ExtensionFromContentType tests ────────────────────────────────────────

    [AvaloniaFact(Timeout = 10_000)]
    public Task ExtensionFromContentType_Json_ReturnsJsonExtension()
    {
        ResponseActionsViewModel.ExtensionFromContentType("application/json")
            .Should().Be(".json");
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task ExtensionFromContentType_Xml_ReturnsXmlExtension()
    {
        ResponseActionsViewModel.ExtensionFromContentType("application/xml")
            .Should().Be(".xml");
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task ExtensionFromContentType_Html_ReturnsHtmlExtension()
    {
        ResponseActionsViewModel.ExtensionFromContentType("text/html")
            .Should().Be(".html");
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task ExtensionFromContentType_Markdown_ReturnsMdExtension()
    {
        ResponseActionsViewModel.ExtensionFromContentType("text/markdown")
            .Should().Be(".md");
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task ExtensionFromContentType_EmptyString_ReturnsTxtFallback()
    {
        ResponseActionsViewModel.ExtensionFromContentType(string.Empty)
            .Should().Be(".txt");
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task ExtensionFromContentType_ProblemJson_ReturnsJsonExtension()
    {
        ResponseActionsViewModel.ExtensionFromContentType("application/problem+json")
            .Should().Be(".json");
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task ExtensionFromContentType_AtomXml_ReturnsXmlExtension()
    {
        ResponseActionsViewModel.ExtensionFromContentType("application/atom+xml")
            .Should().Be(".xml");
        return Task.CompletedTask;
    }

    // ── DetectExtensionFromContent tests ──────────────────────────────────────

    [AvaloniaFact(Timeout = 10_000)]
    public Task DetectExtensionFromContent_JsonObject_ReturnsJsonExtension()
    {
        ResponseActionsViewModel.DetectExtensionFromContent("{\"key\":\"value\"}")
            .Should().Be(".json");
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task DetectExtensionFromContent_JsonArray_ReturnsJsonExtension()
    {
        ResponseActionsViewModel.DetectExtensionFromContent("[1,2,3]")
            .Should().Be(".json");
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task DetectExtensionFromContent_XmlContent_ReturnsXmlExtension()
    {
        ResponseActionsViewModel.DetectExtensionFromContent("<root><child/></root>")
            .Should().Be(".xml");
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task DetectExtensionFromContent_PlainText_ReturnsTxtFallback()
    {
        ResponseActionsViewModel.DetectExtensionFromContent("hello world")
            .Should().Be(".txt");
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task DetectExtensionFromContent_LeadingWhitespace_IsStrippedBeforeDetection()
    {
        ResponseActionsViewModel.DetectExtensionFromContent("  {\"ok\":true}")
            .Should().Be(".json");
        return Task.CompletedTask;
    }

    // ── TryGetSaveableResponseContent tests ───────────────────────────────────

    [AvaloniaFact(Timeout = 10_000)]
    public Task TryGetSaveableResponseContent_BodyTabWithJson_ReturnsBodyAndJsonExtension()
    {
        var context = new StubResponseActionsContext
        {
            SelectedResponseTabIndex = 0,
            ResponseBody = "{\"ok\":true}",
            ResponseContentType = "application/json"
        };
        var viewModel = new ResponseActionsViewModel(context);

        var result = viewModel.TryGetSaveableResponseContent(out var content, out var extension);

        result.Should().BeTrue();
        content.Should().Be("{\"ok\":true}");
        extension.Should().Be(".json");
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task TryGetSaveableResponseContent_BodyTabEmpty_ReturnsFalse()
    {
        var context = new StubResponseActionsContext
        {
            SelectedResponseTabIndex = 0,
            ResponseBody = string.Empty
        };
        var viewModel = new ResponseActionsViewModel(context);

        var result = viewModel.TryGetSaveableResponseContent(out _, out _);

        result.Should().BeFalse();
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task TryGetSaveableResponseContent_WebViewTab_ReturnsFalse()
    {
        var context = new StubResponseActionsContext
        {
            SelectedResponseTabIndex = 4,
            ResponseBody = "<html></html>"
        };
        var viewModel = new ResponseActionsViewModel(context);

        var result = viewModel.TryGetSaveableResponseContent(out var content, out var extension);

        result.Should().BeFalse();
        content.Should().BeEmpty();
        extension.Should().Be(".txt");
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task TryGetSaveableResponseContent_HeadersTabWithEntries_ReturnsJoinedHeaders()
    {
        var context = new StubResponseActionsContext
        {
            SelectedResponseTabIndex = 2
        };
        context.ResponseHeadersList.Add("Content-Type: application/json");
        context.ResponseHeadersList.Add("X-Custom: value");
        var viewModel = new ResponseActionsViewModel(context);

        var result = viewModel.TryGetSaveableResponseContent(out var content, out var extension);

        result.Should().BeTrue();
        content.Should().Be($"Content-Type: application/json{Environment.NewLine}X-Custom: value");
        extension.Should().Be(".txt");
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task TryGetSaveableResponseContent_HeadersTabEmpty_ReturnsFalse()
    {
        var context = new StubResponseActionsContext { SelectedResponseTabIndex = 2 };
        var viewModel = new ResponseActionsViewModel(context);

        var result = viewModel.TryGetSaveableResponseContent(out _, out _);

        result.Should().BeFalse();
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task TryGetSaveableResponseContent_RawTabWithContent_ReturnsRawTextAndExtension()
    {
        var context = new StubResponseActionsContext
        {
            SelectedResponseTabIndex = 3,
            ResponseRawText = "HTTP/1.1 200 OK\r\n\r\n{\"ok\":true}",
            ResponseContentType = "application/json"
        };
        var viewModel = new ResponseActionsViewModel(context);

        var result = viewModel.TryGetSaveableResponseContent(out var content, out var extension);

        result.Should().BeTrue();
        content.Should().Be("HTTP/1.1 200 OK\r\n\r\n{\"ok\":true}");
        extension.Should().Be(".json");
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task TryGetSaveableResponseContent_RawTabEmpty_ReturnsFalse()
    {
        var context = new StubResponseActionsContext
        {
            SelectedResponseTabIndex = 3,
            ResponseRawText = string.Empty
        };
        var viewModel = new ResponseActionsViewModel(context);

        var result = viewModel.TryGetSaveableResponseContent(out _, out _);

        result.Should().BeFalse();
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task TryGetSaveableResponseContent_BodyTabNoContentType_DetectsExtensionFromContent()
    {
        var context = new StubResponseActionsContext
        {
            SelectedResponseTabIndex = 0,
            ResponseBody = "{\"data\":42}",
            ResponseContentType = string.Empty
        };
        var viewModel = new ResponseActionsViewModel(context);

        var result = viewModel.TryGetSaveableResponseContent(out _, out var extension);

        result.Should().BeTrue();
        extension.Should().Be(".json");
        return Task.CompletedTask;
    }

    // ── OpenResponseBodyInExternalEditor_SmokeTest ────────────────────────────

    [AvaloniaFact(Timeout = 10_000)]
    public async Task OpenResponseBodyInExternalEditorAsync_EmptyBody_DoesNotThrow()
    {
        var context = new StubResponseActionsContext
        {
            ResponseBody = string.Empty,
            ResponseContentType = string.Empty
        };
        var viewModel = new ResponseActionsViewModel(context);

        // Should complete without throwing even when body is empty.
        var act = async () => await viewModel.OpenResponseBodyInExternalEditorCommand.Execute();

        await act.Should().NotThrowAsync();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task OpenResponseBodyInExternalEditorAsync_RecordsTempFile()
    {
        var context = new StubResponseActionsContext
        {
            ResponseBody = "{\"test\":true}",
            ResponseContentType = "application/json"
        };
        var viewModel = new ResponseActionsViewModel(context);

        await viewModel.OpenResponseBodyInExternalEditorCommand.Execute();

        context.RecordedTempFiles.Should().HaveCount(1);
        context.RecordedTempFiles[0].Should().EndWith(".json");
    }

    // ── DeleteTempFiles tests ──────────────────────────────────────────────────

    [Fact]
    public void DeleteTempFiles_ExistingFiles_DeletesAllOfThem()
    {
        var first = Path.GetTempFileName();
        var second = Path.GetTempFileName();

        ResponseActionsViewModel.DeleteTempFiles([first, second]);

        File.Exists(first).Should().BeFalse();
        File.Exists(second).Should().BeFalse();
    }

    [Fact]
    public void DeleteTempFiles_MissingFile_DoesNotThrow()
    {
        var missing = Path.Join(Path.GetTempPath(), $"arbor-missing-{Guid.NewGuid():N}.tmp");

        var act = () => ResponseActionsViewModel.DeleteTempFiles([missing]);

        act.Should().NotThrow();
    }

    [Fact]
    public void DeleteTempFiles_EmptyList_DoesNotThrow()
    {
        var act = () => ResponseActionsViewModel.DeleteTempFiles([]);

        act.Should().NotThrow();
    }

    // ── Test stubs ────────────────────────────────────────────────────────────

    private sealed class StubResponseActionsContext : IResponseActionsContext
    {
        public List<string> ResponseHeadersList { get; } = [];
        public List<string> RecordedTempFiles { get; } = [];

        public IClipboard? Clipboard { get; set; }
        public IStorageProvider? StorageProvider { get; set; }
        public string ResponseBody { get; set; } = string.Empty;
        public string RawResponseBody { get; set; } = string.Empty;
        public string ResponseContentType { get; set; } = string.Empty;
        public string ResponseRawText { get; set; } = string.Empty;
        public IReadOnlyList<string> ResponseHeaders => ResponseHeadersList;
        public int SelectedResponseTabIndex { get; set; }
        public bool IsBinaryResponse { get; set; }
        public string ResponseSaveDefaultFolder { get; set; } = string.Empty;
        public string ResponseSaveFileNamePattern { get; set; } = string.Empty;
        public string SelectedCollectionName { get; set; } = string.Empty;
        public string RequestEditorResolvedUrl { get; set; } = string.Empty;
        public string RequestEditorRequestName { get; set; } = string.Empty;
        public string RequestEditorContentType { get; set; } = string.Empty;
        public ResolvedHttpRequestDraft? ResolvedRequest { get; set; }

        private byte[] _lastResponseBodyBytes = [];

        public byte[] GetLastResponseBodyBytes() => _lastResponseBodyBytes;

        public void SetLastResponseBodyBytes(byte[] bytes) => _lastResponseBodyBytes = bytes;

        public ResolvedHttpRequestDraft BuildResolvedHttpRequestDraft() =>
            ResolvedRequest ?? new ResolvedHttpRequestDraft("Request", "GET", RequestEditorResolvedUrl, null, [], null, true, null, null, null);

        public void RecordTempFile(string path) => RecordedTempFiles.Add(path);

        public void SetResponseSaveFileNamePatternValidationError(string validationMessage) { }
    }
}

