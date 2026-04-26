using Arbor.HttpClient.Core.HttpRequest;

namespace Arbor.HttpClient.Core.Tests;

public class CurlFormatterTests
{
    [Fact]
    public void Format_ShouldEmitSimpleGetCurl()
    {
        var command = CurlFormatter.Format("GET", "http://localhost:5000/api");

        command.Should().Be("curl -X GET 'http://localhost:5000/api'");
    }

    [Fact]
    public void Format_ShouldUppercaseMethod()
    {
        var command = CurlFormatter.Format("post", "http://localhost:5000/");

        command.Should().StartWith("curl -X POST ");
    }

    [Fact]
    public void Format_ShouldDefaultToGetWhenMethodMissing()
    {
        var command = CurlFormatter.Format(" ", "http://localhost:5000/");

        command.Should().StartWith("curl -X GET ");
    }

    [Fact]
    public void Format_ShouldIncludeEnabledHeaders()
    {
        var headers = new[]
        {
            new RequestHeader("Accept", "application/json"),
            new RequestHeader("X-Disabled", "nope", IsEnabled: false),
            new RequestHeader("Authorization", "Bearer abc")
        };

        var command = CurlFormatter.Format("GET", "http://localhost:5000/", null, headers);

        command.Should().Contain("-H 'Accept: application/json'");
        command.Should().Contain("-H 'Authorization: Bearer abc'");
        command.Should().NotContain("X-Disabled");
    }

    [Fact]
    public void Format_ShouldEscapeSingleQuotesInUrlAndBody()
    {
        var command = CurlFormatter.Format(
            "POST",
            "http://localhost:5000/search?q=o'brien",
            "{\"name\":\"O'Brien\"}");

        command.Should().Contain("'http://localhost:5000/search?q=o'\\''brien'");
        command.Should().Contain("--data-raw '{\"name\":\"O'\\''Brien\"}'");
    }

    [Fact]
    public void Format_ShouldIncludeBodyAsDataRaw()
    {
        var command = CurlFormatter.Format(
            "POST",
            "http://localhost:5000/",
            "{\"hello\":\"world\"}");

        command.Should().Contain("--data-raw '{\"hello\":\"world\"}'");
    }

    [Fact]
    public void Format_ShouldOmitBodyWhenNullOrEmpty()
    {
        var command = CurlFormatter.Format("POST", "http://localhost:5000/", string.Empty);

        command.Should().NotContain("--data-raw");
    }

    [Fact]
    public void Format_ShouldThrowWhenUrlIsMissing()
    {
        Action act = () => CurlFormatter.Format("GET", string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Format_FromSavedRequest_ShouldUseMethodUrlAndBody()
    {
        var saved = new SavedRequest(
            Name: "Echo",
            Method: "POST",
            Url: "http://localhost:5000/echo",
            Body: "payload",
            CreatedAtUtc: DateTimeOffset.UnixEpoch);

        var command = CurlFormatter.Format(saved);

        command.Should().Be("curl -X POST 'http://localhost:5000/echo' --data-raw 'payload'");
    }
}
