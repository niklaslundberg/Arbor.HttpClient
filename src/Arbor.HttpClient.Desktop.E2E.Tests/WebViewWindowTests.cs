using Arbor.HttpClient.Desktop.Features.WebView;

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
    public void WebViewWindow_NavigationBarOverflow_UsesHorizontalScrollViewer()
    {
        var xamlPath = FindRepoRootPath("src", "Arbor.HttpClient.Desktop", "Features", "WebView", "WebViewWindow.axaml");
        var xaml = File.ReadAllText(xamlPath);

        xaml.Should().Contain("x:Name=\"NavigationScrollViewer\"");
        xaml.Should().Contain("HorizontalScrollBarVisibility=\"Auto\"");
        xaml.Should().Contain("VerticalScrollBarVisibility=\"Disabled\"");
    }

    private static string FindRepoRootPath(params string[] relativeSegments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is { })
        {
            var slnPath = Path.Combine(current.FullName, "Arbor.HttpClient.slnx");
            if (File.Exists(slnPath))
            {
                return Path.Combine([current.FullName, .. relativeSegments]);
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test base directory.");
    }
}
