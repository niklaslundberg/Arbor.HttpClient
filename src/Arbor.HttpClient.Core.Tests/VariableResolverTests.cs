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
        var variables = new List<EnvironmentVariable> { new("baseUrl", "http://localhost:5000") };
        var result = _resolver.Resolve("{{baseUrl}}/users", variables);
        result.Should().Be("http://localhost:5000/users");
    }

    [Fact]
    public void Resolve_ShouldReplaceUnknownTokenWithEmptyString()
    {
        var variables = new List<EnvironmentVariable> { new("other", "x") };
        var result = _resolver.Resolve("{{baseUrl}}/users", variables);
        result.Should().Be("/users");
    }

    [Fact]
    public void Resolve_ShouldReplaceMultipleTokens()
    {
        var variables = new List<EnvironmentVariable>
        {
            new("host", "localhost"),
            new("token", "abc123")
        };
        var result = _resolver.Resolve("https://{{host}}/auth?key={{token}}", variables);
        result.Should().Be("https://localhost/auth?key=abc123");
    }

    [Fact]
    public void Resolve_ShouldBeCaseInsensitive()
    {
        var variables = new List<EnvironmentVariable> { new("ApiKey", "secret") };
        var result = _resolver.Resolve("Bearer {{apikey}}", variables);
        result.Should().Be("Bearer secret");
    }

    [Fact]
    public void Resolve_ShouldReplaceTokenWithEmptyStringWhenNoVariables()
    {
        var result = _resolver.Resolve("{{baseUrl}}/path", []);
        result.Should().Be("/path");
    }

    [Fact]
    public void Resolve_ShouldReturnEmptyStringUnchanged()
    {
        var result = _resolver.Resolve(string.Empty, [new("x", "y")]);
        result.Should().BeEmpty();
    }

    // --- additional coverage for stated requirements ---

    /// <summary>
    /// Exact scenario from the problem statement: {{hello}} is used in the URL but the environment
    /// has no such variable, so it should evaluate to an empty string.
    /// </summary>
    [Fact]
    public void Resolve_UndefinedVariableInUrl_ShouldEvaluateToEmptyString()
    {
        var result = _resolver.Resolve("http://local/{{hello}}", []);
        result.Should().Be("http://local/");
    }

    /// <summary>
    /// Same scenario but with a non-empty environment that simply doesn't contain the token.
    /// </summary>
    [Fact]
    public void Resolve_UndefinedVariableWithOtherVariablesDefined_ShouldEvaluateToEmptyString()
    {
        var variables = new List<EnvironmentVariable> { new("world", "earth") };
        var result = _resolver.Resolve("http://local/{{hello}}", variables);
        result.Should().Be("http://local/");
    }

    /// <summary>
    /// All tokens in the URL are undefined — the entire path collapses to empty strings.
    /// </summary>
    [Fact]
    public void Resolve_AllTokensUndefined_ShouldReplaceAllWithEmptyString()
    {
        var result = _resolver.Resolve("http://{{host}}/{{path}}", []);
        result.Should().Be("http:///");
    }
    [Fact]
    public void Resolve_ProblemStatementScenario_ShouldEvaluateVariableInQueryString()
    {
        var variables = new List<EnvironmentVariable> { new("abc", "123") };
        var result = _resolver.Resolve("http://demo.local?hello={{abc}}", variables);
        result.Should().Be("http://demo.local?hello=123");
    }

    /// <summary>
    /// Tokens with spaces inside the braces (e.g. "{{ abc }}") are trimmed and still matched.
    /// </summary>
    [Fact]
    public void Resolve_ShouldTrimWhitespaceInsideToken()
    {
        var variables = new List<EnvironmentVariable> { new("abc", "123") };
        var result = _resolver.Resolve("http://demo.local?hello={{ abc }}", variables);
        result.Should().Be("http://demo.local?hello=123");
    }

    /// <summary>
    /// The same token appearing more than once in the input is replaced in every occurrence.
    /// </summary>
    [Fact]
    public void Resolve_ShouldReplaceRepeatedTokenEveryOccurrence()
    {
        var variables = new List<EnvironmentVariable> { new("env", "prod") };
        var result = _resolver.Resolve("{{env}}.api.com/{{env}}/v1", variables);
        result.Should().Be("prod.api.com/prod/v1");
    }

    /// <summary>
    /// Variable resolution works in HTTP header values (e.g. Authorization: Bearer {{token}}).
    /// </summary>
    [Fact]
    public void Resolve_ShouldResolveVariablesInHeaderValue()
    {
        var variables = new List<EnvironmentVariable> { new("token", "mySecret") };
        var result = _resolver.Resolve("Bearer {{token}}", variables);
        result.Should().Be("Bearer mySecret");
    }

    /// <summary>
    /// Variable resolution works in HTTP header names (e.g. X-{{tenant}}: value).
    /// </summary>
    [Fact]
    public void Resolve_ShouldResolveVariablesInHeaderName()
    {
        var variables = new List<EnvironmentVariable> { new("tenant", "Acme") };
        var result = _resolver.Resolve("X-{{tenant}}", variables);
        result.Should().Be("X-Acme");
    }

    /// <summary>
    /// Variable resolution works inside a JSON body string.
    /// </summary>
    [Fact]
    public void Resolve_ShouldResolveVariablesInJsonBody()
    {
        var variables = new List<EnvironmentVariable>
        {
            new("apiKey", "key-abc"),
            new("environment", "staging")
        };
        var body = """{"apiKey":"{{apiKey}}","env":"{{environment}}"}""";
        var result = _resolver.Resolve(body, variables);
        result.Should().Be("""{"apiKey":"key-abc","env":"staging"}""");
    }

    /// <summary>
    /// When a variable value is an empty string the token is still replaced (not left as-is).
    /// </summary>
    [Fact]
    public void Resolve_ShouldReplaceTokenWithEmptyValueVariable()
    {
        var variables = new List<EnvironmentVariable> { new("empty", string.Empty) };
        var result = _resolver.Resolve("prefix{{empty}}suffix", variables);
        result.Should().Be("prefixsuffix");
    }

    /// <summary>
    /// A mix of known and unknown tokens: known ones are replaced, unknown ones become empty strings.
    /// </summary>
    [Fact]
    public void Resolve_ShouldReplaceKnownAndCollapseUnknownTokensInSameInput()
    {
        var variables = new List<EnvironmentVariable> { new("host", "localhost") };
        var result = _resolver.Resolve("https://{{host}}/{{version}}/users", variables);
        result.Should().Be("https://localhost//users");
    }
}

