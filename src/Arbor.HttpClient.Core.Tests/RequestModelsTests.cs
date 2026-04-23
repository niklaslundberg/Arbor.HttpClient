using Arbor.HttpClient.Core.Models;
using AwesomeAssertions;

namespace Arbor.HttpClient.Core.Tests;

public class RequestModelsTests
{
    [Fact]
    public void HttpRequestDraft_ShouldDefaultFollowRedirectsToNull()
    {
        var draft = new HttpRequestDraft("name", "GET", "https://example.com", null);

        draft.FollowRedirects.Should().BeNull();
    }

    [Fact]
    public void HttpRequestDraft_ShouldStoreFollowRedirectOverride()
    {
        var draft = new HttpRequestDraft("name", "GET", "https://example.com", null, FollowRedirects: false);

        draft.FollowRedirects.Should().BeFalse();
    }

    [Fact]
    public void ScheduledJobConfig_ShouldStoreFollowRedirectOverride()
    {
        var config = new ScheduledJobConfig(1, "job", "GET", "https://example.com", null, null, 30, AutoStart: true, FollowRedirects: true);

        config.FollowRedirects.Should().BeTrue();
    }

    [Fact]
    public void ScheduledJobConfig_ShouldStoreAllProperties()
    {
        var config = new ScheduledJobConfig(
            Id: 42,
            Name: "Test Job",
            Method: "POST",
            Url: "https://api.example.com/endpoint",
            Body: "{\"key\":\"value\"}",
            HeadersJson: "[{\"Key\":\"Authorization\",\"Value\":\"Bearer token\"}]",
            IntervalSeconds: 60,
            AutoStart: false,
            FollowRedirects: null);

        config.Id.Should().Be(42);
        config.Name.Should().Be("Test Job");
        config.Method.Should().Be("POST");
        config.Url.Should().Be("https://api.example.com/endpoint");
        config.Body.Should().Be("{\"key\":\"value\"}");
        config.HeadersJson.Should().Be("[{\"Key\":\"Authorization\",\"Value\":\"Bearer token\"}]");
        config.IntervalSeconds.Should().Be(60);
        config.AutoStart.Should().BeFalse();
        config.FollowRedirects.Should().BeNull();
    }

    [Fact]
    public void SavedRequest_ShouldStoreAllProperties()
    {
        var createdAt = new DateTimeOffset(2026, 4, 23, 14, 30, 0, TimeSpan.Zero);
        var request = new SavedRequest("My Request", "DELETE", "https://example.com/resource/1", "{\"confirm\":true}", createdAt);

        request.Name.Should().Be("My Request");
        request.Method.Should().Be("DELETE");
        request.Url.Should().Be("https://example.com/resource/1");
        request.Body.Should().Be("{\"confirm\":true}");
        request.CreatedAtUtc.Should().Be(createdAt);
    }

    [Fact]
    public void SavedRequest_CreatedAtLocalDisplay_ShouldFormatCorrectly()
    {
        var createdAt = new DateTimeOffset(2026, 4, 23, 14, 30, 45, TimeSpan.Zero);
        var request = new SavedRequest("Test", "GET", "https://example.com", null, createdAt);

        // The display should be in local time with format "yyyy-MM-dd HH:mm:ss"
        request.CreatedAtLocalDisplay.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$");
    }

    [Fact]
    public void RequestEnvironment_ShouldStoreAllProperties()
    {
        var variables = new List<EnvironmentVariable>
        {
            new("baseUrl", "https://api.example.com", IsEnabled: true),
            new("apiKey", "secret-key-123", IsEnabled: false)
        };

        var env = new RequestEnvironment(10, "Production", variables);

        env.Id.Should().Be(10);
        env.Name.Should().Be("Production");
        env.Variables.Should().HaveCount(2);
        env.Variables[0].Name.Should().Be("baseUrl");
        env.Variables[1].Name.Should().Be("apiKey");
    }

    [Fact]
    public void Collection_ShouldStoreAllProperties()
    {
        var requests = new List<CollectionRequest>
        {
            new("Get Users", "GET", "/users", "Retrieves all users"),
            new("Create User", "POST", "/users", "Creates a new user", "Requires admin role")
        };

        var collection = new Collection(42, "My Collection", "/path/to/source", "https://api.example.com", requests);

        collection.Id.Should().Be(42);
        collection.Name.Should().Be("My Collection");
        collection.SourcePath.Should().Be("/path/to/source");
        collection.BaseUrl.Should().Be("https://api.example.com");
        collection.Requests.Should().HaveCount(2);
        collection.Requests[0].Name.Should().Be("Get Users");
        collection.Requests[1].Name.Should().Be("Create User");
    }

    [Fact]
    public void Collection_ShouldAllowNullSourcePath()
    {
        var collection = new Collection(1, "Test", null, "https://example.com", new List<CollectionRequest>());

        collection.SourcePath.Should().BeNull();
    }

    [Fact]
    public void Collection_ShouldAllowNullBaseUrl()
    {
        var collection = new Collection(1, "Test", "/path", null, new List<CollectionRequest>());

        collection.BaseUrl.Should().BeNull();
    }

    [Fact]
    public void HttpRequestDiagnostics_ShouldStoreAllProperties()
    {
        var diagnostics = new HttpRequestDiagnostics(
            "POST",
            "https://api.example.com/endpoint",
            "1.1",
            "2.0",
            "127.0.0.1",
            "Tls12",
            5.5,
            12.3,
            45.7,
            102.4,
            165.9);

        diagnostics.Method.Should().Be("POST");
        diagnostics.Url.Should().Be("https://api.example.com/endpoint");
        diagnostics.RequestedHttpVersion.Should().Be("1.1");
        diagnostics.ResponseHttpVersion.Should().Be("2.0");
        diagnostics.DnsLookup.Should().Be("127.0.0.1");
        diagnostics.TlsNegotiation.Should().Be("Tls12");
        diagnostics.DnsLookupMilliseconds.Should().Be(5.5);
        diagnostics.TlsNegotiationMilliseconds.Should().Be(12.3);
        diagnostics.ResponseHeadersMilliseconds.Should().Be(45.7);
        diagnostics.ResponseBodyMilliseconds.Should().Be(102.4);
        diagnostics.TotalMilliseconds.Should().Be(165.9);
    }

    [Fact]
    public void HttpRequestDiagnostics_ShouldHandleZeroTimings()
    {
        var diagnostics = new HttpRequestDiagnostics("GET", "https://example.com", "1.1", "1.1", "Skipped", "Not applicable", 0, 0, 0, 0, 0);

        diagnostics.DnsLookupMilliseconds.Should().Be(0);
        diagnostics.TlsNegotiationMilliseconds.Should().Be(0);
        diagnostics.TotalMilliseconds.Should().Be(0);
    }
}
