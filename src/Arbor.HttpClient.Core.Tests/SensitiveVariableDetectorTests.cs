using Arbor.HttpClient.Core.Environments;

namespace Arbor.HttpClient.Core.Tests;

public class SensitiveVariableDetectorTests
{
    [Theory]
    [InlineData("password")]
    [InlineData("Password")]
    [InlineData("PASSWORD")]
    [InlineData("db_password")]
    [InlineData("user_passwd")]
    [InlineData("token")]
    [InlineData("access_token")]
    [InlineData("refresh_token")]
    [InlineData("apiKey")]
    [InlineData("api_key")]
    [InlineData("API_KEY")]
    [InlineData("secret")]
    [InlineData("client_secret")]
    [InlineData("authToken")]
    [InlineData("bearer_token")]
    [InlineData("private_key")]
    [InlineData("signing_key")]
    [InlineData("encryption_key")]
    [InlineData("passphrase")]
    public void IsSensitive_KnownSensitiveNames_ReturnsTrue(string name)
    {
        SensitiveVariableDetector.IsSensitive(name).Should().BeTrue(
            because: $"'{name}' matches a known sensitive keyword");
    }

    [Theory]
    [InlineData("baseUrl")]
    [InlineData("host")]
    [InlineData("port")]
    [InlineData("timeout")]
    [InlineData("retries")]
    [InlineData("environment")]
    [InlineData("apiVersion")]
    [InlineData("region")]
    public void IsSensitive_NonSensitiveNames_ReturnsFalse(string name)
    {
        SensitiveVariableDetector.IsSensitive(name).Should().BeFalse(
            because: $"'{name}' does not match any sensitive keyword");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IsSensitive_EmptyOrWhitespace_ReturnsFalse(string name)
    {
        SensitiveVariableDetector.IsSensitive(name).Should().BeFalse();
    }
}
