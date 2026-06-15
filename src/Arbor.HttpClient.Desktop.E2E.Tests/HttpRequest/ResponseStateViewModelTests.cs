using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Desktop.Features.HttpRequest;

namespace Arbor.HttpClient.Desktop.E2E.Tests.HttpRequest;

[Trait("Category", "Integration")]
public class ResponseStateViewModelTests
{
    private static HttpResponseDetails SampleResponse() =>
        new(
            StatusCode: 200,
            ReasonPhrase: "OK",
            Body: "hello world",
            Headers: new (string Name, string Value)[] { ("Content-Type", "text/plain") },
            ElapsedMilliseconds: 12);

    [Fact]
    public void Apply_PopulatesStatusBodyAndHeaders()
    {
        var viewModel = new ResponseStateViewModel();

        viewModel.Apply(SampleResponse());

        viewModel.ResponseStatusCode.Should().Be(200);
        viewModel.HasResponseHeaders.Should().BeTrue();
        viewModel.ResponseHeaders.Should().NotBeEmpty();
        viewModel.RawResponseBody.Should().Contain("hello world");
    }

    [Fact]
    public void CaptureSnapshot_ThenRestore_RoundTripsState()
    {
        var viewModel = new ResponseStateViewModel();
        viewModel.Apply(SampleResponse());
        var snapshot = viewModel.CaptureSnapshot();

        viewModel.Clear();
        viewModel.ResponseStatusCode.Should().Be(0);

        viewModel.Restore(snapshot);

        viewModel.ResponseStatusCode.Should().Be(200);
        viewModel.RawResponseBody.Should().Contain("hello world");
    }

    [Fact]
    public void Restore_WithNullSnapshot_ClearsState()
    {
        var viewModel = new ResponseStateViewModel();
        viewModel.Apply(SampleResponse());

        viewModel.Restore(null);

        viewModel.ResponseStatusCode.Should().Be(0);
        viewModel.ResponseHeaders.Should().BeEmpty();
        viewModel.ResponseWebViewUri.Should().Be("about:blank");
    }

    [Fact]
    public void ClearMetadata_ResetsOnlyStatusTimingAndSize()
    {
        var viewModel = new ResponseStateViewModel();
        viewModel.Apply(SampleResponse());

        viewModel.ClearMetadata();

        viewModel.ResponseStatusCode.Should().Be(0);
        viewModel.ResponseTimeDisplay.Should().BeEmpty();
        viewModel.ResponseSizeDisplay.Should().BeEmpty();
        // Body is preserved.
        viewModel.RawResponseBody.Should().Contain("hello world");
    }

    [Fact]
    public void Clear_ResetsToDefaults()
    {
        var viewModel = new ResponseStateViewModel();
        viewModel.Apply(SampleResponse());

        viewModel.Clear();

        viewModel.ResponseStatusCode.Should().Be(0);
        viewModel.ResponseBody.Should().BeEmpty();
        viewModel.RawResponseBody.Should().BeEmpty();
        viewModel.ResponseBodyTabLabel.Should().Be("Body");
        viewModel.ResponseHeaders.Should().BeEmpty();
        viewModel.GetLastResponseBodyBytes().Should().BeEmpty();
    }
}
