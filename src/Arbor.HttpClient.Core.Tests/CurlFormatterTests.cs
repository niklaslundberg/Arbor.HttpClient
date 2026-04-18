using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using AwesomeAssertions;

namespace Arbor.HttpClient.Core.Tests;

public class CurlFormatterTests
{
    [Fact]
    public void Format_ShouldEmitSimpleGetCurl()
    {
        var command = CurlFormatter.Format("GET", "https://example.com/api");

        command.Should().Be("curl -X GET 'https://example.com/api'");
    }

    [Fact]
    public void Format_ShouldUppercaseMethod()
    {
        var command = CurlFormatter.Format("post", "https://example.com/");

        command.Should().StartWith("curl -X POST ");
    }

    [Fact]
    public void Format_ShouldDefaultToGetWhenMethodMissing()
    {
        var command = CurlFormatter.Format(" ", "https://example.com/");

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

        var command = CurlFormatter.Format("GET", "https://example.com/", null, headers);

        command.Should().Contain("-H 'Accept: application/json'");
        command.Should().Contain("-H 'Authorization: Bearer abc'");
        command.Should().NotContain("X-Disabled");
    }

    [Fact]
    public void Format_ShouldEscapeSingleQuotesInUrlAndBody()
    {
        var command = CurlFormatter.Format(
            "POST",
            "https://example.com/search?q=o'brien",
            "{\"name\":\"O'Brien\"}");

        command.Should().Contain("'https://example.com/search?q=o'\\''brien'");
        command.Should().Contain("--data-raw '{\"name\":\"O'\\''Brien\"}'");
    }

    [Fact]
    public void Format_ShouldIncludeBodyAsDataRaw()
    {
        var command = CurlFormatter.Format(
            "POST",
            "https://example.com/",
            "{\"hello\":\"world\"}");

        command.Should().Contain("--data-raw '{\"hello\":\"world\"}'");
    }

    [Fact]
    public void Format_ShouldOmitBodyWhenNullOrEmpty()
    {
        var command = CurlFormatter.Format("POST", "https://example.com/", string.Empty);

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
            Url: "https://example.com/echo",
            Body: "payload",
            CreatedAtUtc: DateTimeOffset.UnixEpoch);

        var command = CurlFormatter.Format(saved);

        command.Should().Be("curl -X POST 'https://example.com/echo' --data-raw 'payload'");
    }
}
