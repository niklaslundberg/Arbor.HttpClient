using Arbor.HttpClient.Desktop.Features.Main;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public class ResponseSaveFileNamePatternFormatterTests
{
    [Fact]
    public void TryFormat_WithSupportedTokens_ReturnsNormalizedFileName()
    {
        var success = ResponseSaveFileNamePatternFormatter.TryFormat(
            "{collectionName}-{requestPath}-{requestName}-{timestamp:yyyy-MM-dd HH.mm.ss}{contentTypeExtension}",
            "My/Collection",
            "api/v1/users",
            "Get:Users",
            ".json",
            new DateTimeOffset(2026, 05, 09, 13, 45, 00, TimeSpan.Zero),
            out var fileName,
            out var error);

        success.Should().BeTrue();
        error.Should().BeEmpty();
        fileName.Should().Be("My_Collection-api_v1_users-Get_Users-2026-05-09 13.45.00.json");
    }

    [Fact]
    public void TryValidatePattern_WithUnknownToken_ReturnsFalse()
    {
        var success = ResponseSaveFileNamePatternFormatter.TryValidatePattern(
            "{requestName}-{unknownToken}{contentTypeExtension}",
            out var error);

        success.Should().BeFalse();
        error.Should().Contain("Unsupported token");
    }
}
