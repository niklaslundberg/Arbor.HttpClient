using Arbor.HttpClient.Core.Variables;

namespace Arbor.HttpClient.Core.Tests;

/// <summary>
/// Integration tests for <see cref="SystemEnvironmentVariableProvider"/> that exercise the real
/// process environment. These tests mutate process-level state and must not run in parallel with
/// other tests that depend on the same environment keys — hence the
/// <see cref="ProcessEnvironmentCollection"/> collection with <c>DisableParallelization = true</c>.
/// </summary>
[Collection("ProcessEnvironment")]
[Trait("Category", "Integration")]
public class SystemEnvironmentVariableProviderTests
{
    [Fact]
    public void GetAll_ShouldReturnEntryForSetVariable()
    {
        const string key = "ARBOR_HTTP_CLIENT_TEST_VAR";
        const string value = "test_value_42";
        var previous = Environment.GetEnvironmentVariable(key);
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
            Environment.SetEnvironmentVariable(key, previous);
        }
    }

    [Fact]
    public void GetAll_LookupIsCaseInsensitive()
    {
        const string key = "ARBOR_HTTP_CLIENT_CI_TEST";
        const string value = "ci_value";
        var previous = Environment.GetEnvironmentVariable(key);
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
            Environment.SetEnvironmentVariable(key, previous);
        }
    }

    [Fact]
    public void GetAll_ShouldNotContainRemovedVariable()
    {
        const string key = "ARBOR_HTTP_CLIENT_REMOVED_VAR";
        var previous = Environment.GetEnvironmentVariable(key);
        try
        {
            // Set, then explicitly remove the variable so we verify that null/removal is reflected.
            Environment.SetEnvironmentVariable(key, "transient");
            Environment.SetEnvironmentVariable(key, null);

            var provider = new SystemEnvironmentVariableProvider();
            var result = provider.GetAll();

            result.Should().NotContainKey(key);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previous);
        }
    }
}
