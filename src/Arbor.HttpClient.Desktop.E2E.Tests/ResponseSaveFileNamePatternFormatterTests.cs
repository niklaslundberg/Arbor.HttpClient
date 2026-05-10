using Arbor.HttpClient.Desktop.Features.Main;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public class ResponseSaveFileNamePatternFormatterTests
{
    [AvaloniaFact(Timeout = 10_000)]
    public void TryFormat_WithSupportedTokens_ReturnsNormalizedFileName()
    {
        var timestamp = new DateTimeOffset(2026, 05, 09, 13, 45, 00, TimeSpan.FromHours(2));
        var success = ResponseSaveFileNamePatternFormatter.TryFormat(
            "{collectionName}-{requestPath}-{requestName}-{timestamp:yyyy-MM-dd HH.mm.ss}{contentTypeExtension}",
            "My/Collection",
            "api/v1/users",
            "Get:Users",
            ".json",
            timestamp,
            out var fileName,
            out var error);

        success.Should().BeTrue();
        error.Should().BeEmpty();
        fileName.Should().Be($"My_Collection-api_v1_users-Get_Users-{timestamp.ToLocalTime():yyyy-MM-dd HH.mm.ss}.json");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void TryFormat_WithTimestampUtcToken_FormatsUtcValue()
    {
        var timestamp = new DateTimeOffset(2026, 05, 09, 13, 45, 00, TimeSpan.FromHours(2));
        var success = ResponseSaveFileNamePatternFormatter.TryFormat(
            "{timestamp:yyyyMMddHHmmss}-{timestampUtc:yyyyMMddHHmmss}{extension}",
            "Collection",
            "path",
            "request",
            ".txt",
            timestamp,
            out var fileName,
            out var error);

        success.Should().BeTrue();
        error.Should().BeEmpty();
        fileName.Should().Be($"{timestamp.ToLocalTime():yyyyMMddHHmmss}-{timestamp.ToUniversalTime():yyyyMMddHHmmss}.txt");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void TryValidatePattern_WithUnknownToken_ReturnsFalse()
    {
        var success = ResponseSaveFileNamePatternFormatter.TryValidatePattern(
            "{requestName}-{unknownToken}{contentTypeExtension}",
            out var error);

        success.Should().BeFalse();
        error.Should().Contain("Unsupported token");
    }
}

