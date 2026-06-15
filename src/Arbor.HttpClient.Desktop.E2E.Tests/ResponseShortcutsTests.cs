using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Net;
using System.Text;
using Arbor.HttpClient.Desktop;
using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Desktop.Features.Logging;
using Arbor.HttpClient.Desktop.Features.Main;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Arbor.HttpClient.Testing.Fakes;
using Arbor.HttpClient.Testing.Repositories;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input.Platform;
using Serilog;
using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.HttpRequest;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Tests for the UX 2.2 "Copy / save response shortcuts" feature:
/// Copy body to clipboard, Save body as file, Copy as cURL.
/// </summary>
[Collection("HeadlessAvalonia")]
[Trait("Category", "Integration")]
public class ResponseShortcutsTests
{
    private static MainWindowViewModel CreateViewModel(HttpResponseMessage httpResponse)
    {
        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => httpResponse);
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        return new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task HasTextResponse_IsFalse_BeforeAnyRequest()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        using var viewModel = CreateViewModel(response);

        viewModel.HasTextResponse.Should().BeFalse();
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task HasTextResponse_BecomesTrue_AfterTextResponse()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
        };
        using var viewModel = CreateViewModel(response);

        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/api";
        await viewModel.SendRequestCommand.Execute();

        viewModel.HasTextResponse.Should().BeTrue();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task HasTextResponse_RemainsFalse_AfterBinaryResponse()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([0xFF, 0xD8, 0xFF])
        };
        response.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

        using var viewModel = CreateViewModel(response);
        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/image.png";
        await viewModel.SendRequestCommand.Execute();

        viewModel.HasTextResponse.Should().BeFalse();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task CopyResponseBodyCommand_CopiesFormattedBodyToClipboard()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"hello\":\"world\"}", Encoding.UTF8, "application/json")
        };
        using var viewModel = CreateViewModel(response);

        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

        viewModel.Clipboard = TopLevel.GetTopLevel(window)?.Clipboard;
        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/api";
        await viewModel.SendRequestCommand.Execute();

        await viewModel.ResponseActions.CopyResponseBodyCommand.Execute();

        var clipboardText = await (TopLevel.GetTopLevel(window)?.Clipboard?.TryGetTextAsync()
                         ?? Task.FromResult<string?>(null));
        window.Close();

        clipboardText.Should().NotBeNullOrEmpty();
        clipboardText.Should().Contain("hello");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task CopyResponseBodyCommand_DoesNothing_WhenClipboardIsNull()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("test body", Encoding.UTF8, "text/plain")
        };
        using var viewModel = CreateViewModel(response);

        viewModel.Clipboard = null;
        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/api";
        await viewModel.SendRequestCommand.Execute();

        // Should not throw
        await viewModel.ResponseActions.CopyResponseBodyCommand.Execute();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task CopyCurrentRequestAsCurlCommand_CopiesCurlToClipboard()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        using var viewModel = CreateViewModel(response);

        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

        viewModel.Clipboard = TopLevel.GetTopLevel(window)?.Clipboard;
        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/api";
        viewModel.RequestEditor.SelectedMethod = "POST";
        await viewModel.SendRequestCommand.Execute();

        await viewModel.ResponseActions.CopyCurrentRequestAsCurlCommand.Execute();

        var clipboardText = await (TopLevel.GetTopLevel(window)?.Clipboard?.TryGetTextAsync()
                         ?? Task.FromResult<string?>(null));
        window.Close();

        clipboardText.Should().NotBeNullOrEmpty();
        clipboardText.Should().Contain("curl");
        clipboardText.Should().Contain("POST");
        clipboardText.Should().Contain("localhost:5000");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SaveResponseBodyAsFileCommand_DoesNothing_WhenStorageProviderIsNull()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("body text", Encoding.UTF8, "text/plain")
        };
        using var viewModel = CreateViewModel(response);

        viewModel.StorageProvider = null;
        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/api";
        await viewModel.SendRequestCommand.Execute();

        // Should not throw
        await viewModel.ResponseActions.SaveResponseBodyAsFileCommand.Execute();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task ResponseBody_PreservesNonAsciiCharacters_AfterJsonResponse()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"name\":\"åäö\"}", Encoding.UTF8, "application/json")
        };
        using var viewModel = CreateViewModel(response);

        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/api";
        await viewModel.SendRequestCommand.Execute();

        var responseBody = viewModel.ResponseBody;
        responseBody.Should().Contain("åäö");
        responseBody.Should().NotContain("\\u00e5");
        responseBody.Should().NotContain("\\u00e4");
        responseBody.Should().NotContain("\\u00f6");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task TryGetSaveableResponseContent_WhenSelectedTabIsBody_ReturnsBodyAndDetectedExtension()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        using var viewModel = CreateViewModel(response);

        viewModel.SelectedResponseTabIndex = 0;
        viewModel.Response.ResponseBody = "{\"ok\":true}";
        viewModel.Response.ResponseContentType = "application/json";

        var result = viewModel.ResponseActions.TryGetSaveableResponseContent(out var content, out var extension);

        result.Should().BeTrue();
        content.Should().Be("{\"ok\":true}");
        extension.Should().Be(".json");
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task TryGetSaveableResponseContent_WhenSelectedTabIsBodyRaw_ReturnsRawBodyAndExtension()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        using var viewModel = CreateViewModel(response);

        viewModel.SelectedResponseTabIndex = 1;
        viewModel.Response.RawResponseBody = "<raw>";
        viewModel.Response.ResponseContentType = "text/plain";

        var result = viewModel.ResponseActions.TryGetSaveableResponseContent(out var content, out var extension);

        result.Should().BeTrue();
        content.Should().Be("<raw>");
        extension.Should().Be(".txt");
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task TryGetSaveableResponseContent_WhenSelectedTabIsHeaders_ReturnsJoinedHeaders()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        using var viewModel = CreateViewModel(response);

        viewModel.SelectedResponseTabIndex = 2;
        viewModel.ResponseHeaders.Add("Content-Type: application/json");
        viewModel.ResponseHeaders.Add("X-Test: value");

        var result = viewModel.ResponseActions.TryGetSaveableResponseContent(out var content, out var extension);

        result.Should().BeTrue();
        content.Should().Be($"Content-Type: application/json{Environment.NewLine}X-Test: value");
        extension.Should().Be(".txt");
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task TryGetSaveableResponseContent_WhenSelectedTabIsResponseRaw_ReturnsResponseRawText()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        using var viewModel = CreateViewModel(response);

        viewModel.SelectedResponseTabIndex = 3;
        viewModel.Response.ResponseRawText = "HTTP/1.1 200 OK";
        viewModel.Response.ResponseContentType = "application/json";

        var result = viewModel.ResponseActions.TryGetSaveableResponseContent(out var content, out var extension);

        result.Should().BeTrue();
        content.Should().Be("HTTP/1.1 200 OK");
        extension.Should().Be(".json");
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task TryGetSaveableResponseContent_WhenSelectedTabIsWebView_ReturnsFalse()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        using var viewModel = CreateViewModel(response);

        viewModel.SelectedResponseTabIndex = 4;
        viewModel.Response.ResponseBody = "<html></html>";

        var result = viewModel.ResponseActions.TryGetSaveableResponseContent(out var content, out var extension);

        result.Should().BeFalse();
        content.Should().BeEmpty();
        extension.Should().Be(".txt");
        return Task.CompletedTask;
    }

    [AvaloniaTheory(Timeout = 10_000)]
    [InlineData("text/markdown", ".md")]
    [InlineData("application/json", ".json")]
    [InlineData("application/problem+json", ".json")]
    [InlineData("application/xml", ".xml")]
    [InlineData("application/atom+xml", ".xml")]
    public Task ExtensionFromContentType_ShouldReturnExpectedExtension(string contentType, string expectedExtension)
    {
        var extension = ResponseActionsViewModel.ExtensionFromContentType(contentType);

        extension.Should().Be(expectedExtension);
        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task RequestTimeoutDefaultWatermark_ShouldIncludeConfiguredDefaultTimeoutValue()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        using var viewModel = CreateViewModel(response);
        viewModel.DefaultRequestTimeoutSeconds = 42;

        viewModel.RequestTimeoutDefaultWatermark.Should().Be("Default (42)");
        return Task.CompletedTask;
    }
}
