using Arbor.HttpClient.Desktop;
using Arbor.HttpClient.Desktop.Features.WebView;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Skia;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

[Collection("HeadlessAvalonia")]
[Trait("Category", "Integration")]
public class WebViewWindowTests
{
    [Fact]
    public void BuildNoCacheUri_WithNoQuery_AppendsCacheBustParameter()
    {
        var result = WebViewWindow.BuildNoCacheUri(new Uri("https://example.com/page"), 1234);

        result.ToString().Should().Be("https://example.com/page?arborNoCache=1234");
    }

    [Fact]
    public void BuildNoCacheUri_WithExistingQuery_PreservesQueryAndAppendsCacheBustParameter()
    {
        var result = WebViewWindow.BuildNoCacheUri(new Uri("https://example.com/page?foo=bar"), 1234);

        result.ToString().Should().Be("https://example.com/page?foo=bar&arborNoCache=1234");
    }

    [Fact]
    public async Task WebViewWindow_NavigationBarOverflow_UsesHorizontalScrollViewer()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(() =>
        {
            var window = new WebViewWindow();

            var navigationScrollViewer = window.FindControl<ScrollViewer>("NavigationScrollViewer");
            navigationScrollViewer.Should().NotBeNull();
            navigationScrollViewer!.HorizontalScrollBarVisibility.Should().Be(ScrollBarVisibility.Auto);
            navigationScrollViewer.VerticalScrollBarVisibility.Should().Be(ScrollBarVisibility.Disabled);

            return Task.FromResult(true);
        }, CancellationToken.None);
    }

    private sealed class TestEntryPoint
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont()
            .LogToTrace();
    }
}
