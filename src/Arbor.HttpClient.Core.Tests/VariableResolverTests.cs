using System.Text;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Core.Variables;
using Arbor.HttpClient.Testing.Fakes;

namespace Arbor.HttpClient.Core.Tests;

public class VariableResolverTests
{
    private readonly VariableResolver _resolver = new(new FakeSystemEnvironmentVariableProvider());

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

    /// <summary>
    /// <c>{{env:VAR}}</c> tokens are resolved via the injected environment variable provider.
    /// </summary>
    [Fact]
    public void Resolve_ShouldResolveEnvPrefixedToken()
    {
        var envVars = new Dictionary<string, string> { ["MY_VAR"] = "hello" };
        var resolver = new VariableResolver(new FakeSystemEnvironmentVariableProvider(envVars));
        var result = resolver.Resolve("{{env:MY_VAR}}/path", []);
        result.Should().Be("hello/path");
    }

    /// <summary>
    /// Env var lookup is case-insensitive so <c>{{env:my_var}}</c> resolves the same as <c>{{env:MY_VAR}}</c>.
    /// </summary>
    [Fact]
    public void Resolve_EnvTokenLookup_IsCaseInsensitive()
    {
        var envVars = new Dictionary<string, string> { ["MY_VAR"] = "world" };
        var resolver = new VariableResolver(new FakeSystemEnvironmentVariableProvider(envVars));
        var result = resolver.Resolve("{{env:my_var}}", []);
        result.Should().Be("world");
    }

    /// <summary>
    /// When the referenced environment variable does not exist, the token collapses to empty string.
    /// </summary>
    [Fact]
    public void Resolve_UnknownEnvToken_ShouldCollapseToEmptyString()
    {
        var resolver = new VariableResolver(new FakeSystemEnvironmentVariableProvider());
        var result = resolver.Resolve("{{env:NONEXISTENT}}", []);
        result.Should().BeEmpty();
    }

    /// <summary>
    /// Regular app variables and env variables can be mixed in the same input.
    /// </summary>
    [Fact]
    public void Resolve_ShouldResolveMixedAppAndEnvVariables()
    {
        var appVars = new List<EnvironmentVariable> { new("host", "localhost") };
        var envVars = new Dictionary<string, string> { ["PORT"] = "8080" };
        var resolver = new VariableResolver(new FakeSystemEnvironmentVariableProvider(envVars));
        var result = resolver.Resolve("http://{{host}}:{{env:PORT}}/api", appVars);
        result.Should().Be("http://localhost:8080/api");
    }

    /// <summary>
    /// Env prefix matching is case-insensitive so <c>{{ENV:VAR}}</c> is treated the same as <c>{{env:VAR}}</c>.
    /// </summary>
    [Fact]
    public void Resolve_EnvPrefix_IsCaseInsensitive()
    {
        var envVars = new Dictionary<string, string> { ["REGION"] = "eu-west" };
        var resolver = new VariableResolver(new FakeSystemEnvironmentVariableProvider(envVars));
        var result = resolver.Resolve("{{ENV:REGION}}", []);
        result.Should().Be("eu-west");
    }

    /// <summary>
    /// Whitespace after the <c>env:</c> colon is trimmed so <c>{{env: HOME }}</c> still resolves,
    /// consistent with the trimming applied to regular variable tokens.
    /// </summary>
    [Fact]
    public void Resolve_EnvTokenNameWithWhitespace_ShouldTrimAndResolve()
    {
        var envVars = new Dictionary<string, string> { ["HOME"] = "/home/alice" };
        var resolver = new VariableResolver(new FakeSystemEnvironmentVariableProvider(envVars));
        var result = resolver.Resolve("{{env: HOME }}", []);
        result.Should().Be("/home/alice");
    }
}

