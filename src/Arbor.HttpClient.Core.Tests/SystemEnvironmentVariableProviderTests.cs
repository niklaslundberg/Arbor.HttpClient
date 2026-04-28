using Arbor.HttpClient.Core.Variables;

namespace Arbor.HttpClient.Core.Tests;

/// <summary>
/// Tests for <see cref="SystemEnvironmentVariableProvider"/> that exercise the real process environment.
/// </summary>
public class SystemEnvironmentVariableProviderTests
{
    [Fact]
    public void GetAll_ShouldReturnEntryForSetVariable()
    {
        const string key = "ARBOR_HTTP_CLIENT_TEST_VAR";
        const string value = "test_value_42";
        Environment.SetEnvironmentVariable(key, value);
        try
        {
            var provider = new SystemEnvironmentVariableProvider();
            var result = provider.GetAll();

            result.Should().ContainKey(key);
            result[key].Should().Be(value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void GetAll_LookupIsCaseInsensitive()
    {
        const string key = "ARBOR_HTTP_CLIENT_CI_TEST";
        const string value = "ci_value";
        Environment.SetEnvironmentVariable(key, value);
        try
        {
            var provider = new SystemEnvironmentVariableProvider();
            var result = provider.GetAll();

            result.Should().ContainKey(key.ToLowerInvariant());
            result[key.ToLowerInvariant()].Should().Be(value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void GetAll_ShouldNotContainRemovedVariable()
    {
        const string key = "ARBOR_HTTP_CLIENT_REMOVED_VAR";
        Environment.SetEnvironmentVariable(key, "transient");
        Environment.SetEnvironmentVariable(key, null);

        var provider = new SystemEnvironmentVariableProvider();
        var result = provider.GetAll();

        result.Should().NotContainKey(key);
    }
}
