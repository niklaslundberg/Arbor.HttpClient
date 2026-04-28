using Arbor.HttpClient.Core.Environments;

namespace Arbor.HttpClient.Core.Tests;

/// <summary>
/// Tests for <see cref="JwtExpiryExtractor"/>.
/// All test JWTs use a dummy/invalid signature — only the payload is parsed.
/// </summary>
public class JwtExpiryExtractorTests
{
    // A real-format JWT with exp=1893456000 (2030-01-01T00:00:00Z).
    // header  : {"alg":"HS256","typ":"JWT"}
    // payload : {"sub":"test","exp":1893456000}
    // signature: dummy
    private const string ValidJwtWithExp =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9" +
        ".eyJzdWIiOiJ0ZXN0IiwiZXhwIjoxODkzNDU2MDAwfQ" +
        ".SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

    // payload : {"sub":"test"} — no exp claim
    private const string ValidJwtWithoutExp =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9" +
        ".eyJzdWIiOiJ0ZXN0In0" +
        ".SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

    private static readonly DateTimeOffset ExpectedExpiry = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TryGetExpiry_ValidJwtWithExp_ReturnsTrueAndCorrectExpiry()
    {
        var result = JwtExpiryExtractor.TryGetExpiry(ValidJwtWithExp, out var expiry);

        result.Should().BeTrue();
        expiry.Should().NotBeNull();
        expiry!.Value.Should().Be(ExpectedExpiry);
    }

    [Fact]
    public void TryGetExpiry_ValidJwtWithoutExp_ReturnsFalse()
    {
        var result = JwtExpiryExtractor.TryGetExpiry(ValidJwtWithoutExp, out var expiry);

        result.Should().BeFalse();
        expiry.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetExpiry_NullOrWhitespace_ReturnsFalse(string? value)
    {
        var result = JwtExpiryExtractor.TryGetExpiry(value, out var expiry);

        result.Should().BeFalse();
        expiry.Should().BeNull();
    }

    [Theory]
    [InlineData("not.a.jwt.with.extra.dots")]
    [InlineData("only.two")]
    [InlineData("plain text")]
    [InlineData("http://localhost:5000")]
    public void TryGetExpiry_NonJwtValues_ReturnsFalse(string value)
    {
        var result = JwtExpiryExtractor.TryGetExpiry(value, out var expiry);

        result.Should().BeFalse();
        expiry.Should().BeNull();
    }

    [Fact]
    public void TryGetExpiry_ThreePartStringWithInvalidBase64Payload_ReturnsFalse()
    {
        // Three segments but the payload is not valid base64url JSON.
        var result = JwtExpiryExtractor.TryGetExpiry("aaa.!!!.bbb", out var expiry);

        result.Should().BeFalse();
        expiry.Should().BeNull();
    }

    [Fact]
    public void TryGetExpiry_ThreePartStringWithValidBase64ButNotJson_ReturnsFalse()
    {
        // Base64url of "hello world" — valid base64 but not JSON.
        var notJsonPayload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("hello world"))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var result = JwtExpiryExtractor.TryGetExpiry($"aaa.{notJsonPayload}.ccc", out var expiry);

        result.Should().BeFalse();
        expiry.Should().BeNull();
    }

    [Fact]
    public void TryGetExpiry_ExpIsConvertedToUtc()
    {
        var result = JwtExpiryExtractor.TryGetExpiry(ValidJwtWithExp, out var expiry);

        result.Should().BeTrue();
        expiry!.Value.Offset.Should().Be(TimeSpan.Zero);
    }
}
