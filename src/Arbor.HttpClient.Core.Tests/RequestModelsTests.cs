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
}
