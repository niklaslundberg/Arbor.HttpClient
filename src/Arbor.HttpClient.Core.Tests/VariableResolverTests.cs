using System.Text;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using AwesomeAssertions;

namespace Arbor.HttpClient.Core.Tests;

public class VariableResolverTests
{
    private readonly VariableResolver _resolver = new();

    [Fact]
    public void Resolve_ShouldReplaceKnownToken()
    {
        var variables = new List<EnvironmentVariable> { new("baseUrl", "https://api.example.com") };
        var result = _resolver.Resolve("{{baseUrl}}/users", variables);
        result.Should().Be("https://api.example.com/users");
    }

    [Fact]
    public void Resolve_ShouldLeaveUnknownTokenUnchanged()
    {
        var variables = new List<EnvironmentVariable> { new("other", "x") };
        var result = _resolver.Resolve("{{baseUrl}}/users", variables);
        result.Should().Be("{{baseUrl}}/users");
    }

    [Fact]
    public void Resolve_ShouldReplaceMultipleTokens()
    {
        var variables = new List<EnvironmentVariable>
        {
            new("host", "example.com"),
            new("token", "abc123")
        };
        var result = _resolver.Resolve("https://{{host}}/auth?key={{token}}", variables);
        result.Should().Be("https://example.com/auth?key=abc123");
    }

    [Fact]
    public void Resolve_ShouldBeCaseInsensitive()
    {
        var variables = new List<EnvironmentVariable> { new("ApiKey", "secret") };
        var result = _resolver.Resolve("Bearer {{apikey}}", variables);
        result.Should().Be("Bearer secret");
    }

    [Fact]
    public void Resolve_ShouldReturnInputWhenNoVariables()
    {
        var result = _resolver.Resolve("{{baseUrl}}/path", []);
        result.Should().Be("{{baseUrl}}/path");
    }

    [Fact]
    public void Resolve_ShouldReturnEmptyStringUnchanged()
    {
        var result = _resolver.Resolve(string.Empty, [new("x", "y")]);
        result.Should().BeEmpty();
    }
}
