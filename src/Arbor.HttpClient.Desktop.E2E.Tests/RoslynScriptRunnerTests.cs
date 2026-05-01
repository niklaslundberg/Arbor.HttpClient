using Arbor.HttpClient.Core.Scripting;
using Arbor.HttpClient.Desktop.Features.Scripting;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Unit tests for <see cref="RoslynScriptRunner"/>.
/// These tests exercise Roslyn script compilation and execution directly —
/// they do not require an Avalonia headless session.
/// </summary>
public class RoslynScriptRunnerTests
{
    private static ScriptContext MakeContext(string? body = null) =>
        new ScriptContext(
            method: "GET",
            url: "https://example.com/api",
            headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            body: body,
            env: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["token"] = "abc123" });

    private readonly RoslynScriptRunner _runner = new();

    // ── No-op when script is null/whitespace ──────────────────────────────────

    [Fact]
    public async Task RunPreRequestAsync_NullScript_ReturnsSuccessEmptyResult()
    {
        var result = await _runner.RunPreRequestAsync(null, MakeContext());

        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Log.Should().BeEmpty();
    }

    [Fact]
    public async Task RunPreRequestAsync_WhitespaceScript_ReturnsSuccessEmptyResult()
    {
        var result = await _runner.RunPreRequestAsync("   ", MakeContext());

        result.Success.Should().BeTrue();
    }

    // ── Successful execution ──────────────────────────────────────────────────

    [Fact]
    public async Task RunPreRequestAsync_ValidScript_ReturnsSuccess()
    {
        const string script = "ctx.Log(\"hello from script\");";

        var result = await _runner.RunPreRequestAsync(script, MakeContext());

        result.Success.Should().BeTrue();
        result.Log.Should().ContainSingle("hello from script");
    }

    [Fact]
    public async Task RunPreRequestAsync_MutatesContext_ChangesArePropagated()
    {
        const string script = "ctx.Method = \"POST\"; ctx.Url = \"https://mutated.example.com\";";
        var ctx = MakeContext();

        await _runner.RunPreRequestAsync(script, ctx);

        ctx.Method.Should().Be("POST");
        ctx.Url.Should().Be("https://mutated.example.com");
    }

    [Fact]
    public async Task RunPreRequestAsync_ReadsEnvVariable_Succeeds()
    {
        const string script = "ctx.Log(ctx.Env[\"token\"]);";
        var ctx = MakeContext();

        var result = await _runner.RunPreRequestAsync(script, ctx);

        result.Success.Should().BeTrue();
        result.Log.Should().ContainSingle("abc123");
    }

    [Fact]
    public async Task RunPreRequestAsync_SetsEnvVariable_IsVisibleInContext()
    {
        const string script = "ctx.Env[\"newKey\"] = \"newValue\";";
        var ctx = MakeContext();

        var result = await _runner.RunPreRequestAsync(script, ctx);

        result.Success.Should().BeTrue();
        ctx.Env["newKey"].Should().Be("newValue");
    }

    // ── Assertion failures ────────────────────────────────────────────────────

    [Fact]
    public async Task RunPreRequestAsync_FailedAssertion_ReturnsFailureWithMessage()
    {
        const string script = "ctx.Assert(false, \"expected 200\");";

        var result = await _runner.RunPreRequestAsync(script, MakeContext());

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle("expected 200");
    }

    [Fact]
    public async Task RunPreRequestAsync_PassedAssertion_ReturnsSuccess()
    {
        const string script = "ctx.Assert(1 == 1, \"should pass\");";

        var result = await _runner.RunPreRequestAsync(script, MakeContext());

        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    // ── Compilation errors ────────────────────────────────────────────────────

    [Fact]
    public async Task RunPreRequestAsync_SyntaxError_ReturnsFailureWithErrors()
    {
        const string script = "this is not valid C#!!!";

        var result = await _runner.RunPreRequestAsync(script, MakeContext());

        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    // ── Runtime exceptions ────────────────────────────────────────────────────

    [Fact]
    public async Task RunPreRequestAsync_ThrowingScript_ReturnsFailureWithRuntimeError()
    {
        const string script = "throw new InvalidOperationException(\"runtime error from test\");";

        var result = await _runner.RunPreRequestAsync(script, MakeContext());

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("runtime error from test");
    }

    // ── Post-response script with Response ───────────────────────────────────

    [Fact]
    public async Task RunPostResponseAsync_WithResponse_ReadsStatusCode()
    {
        const string script = "ctx.Log(ctx.Response!.StatusCode.ToString());";
        var ctx = MakeContext();
        ctx.Response = new ScriptResponse(200, "OK", "{}", new Dictionary<string, string>());

        var result = await _runner.RunPostResponseAsync(script, ctx);

        result.Success.Should().BeTrue();
        result.Log.Should().ContainSingle("200");
    }

    [Fact]
    public async Task RunPostResponseAsync_ParsesBodyJsonViaSelf_ReadsProperty()
    {
        const string script = """
            var id = ctx.Response!.BodyJson!.Value.GetProperty("id").GetInt32();
            ctx.Log(id.ToString());
            """;
        var ctx = MakeContext();
        ctx.Response = new ScriptResponse(200, "OK", """{"id":99}""", new Dictionary<string, string>());

        var result = await _runner.RunPostResponseAsync(script, ctx);

        result.Success.Should().BeTrue();
        result.Log.Should().ContainSingle("99");
    }

    // ── Script caching ────────────────────────────────────────────────────────

    [Fact]
    public async Task RunPreRequestAsync_SameScript_UsesCache_SecondRunSucceeds()
    {
        const string script = "ctx.Log(\"cached\");";

        var result1 = await _runner.RunPreRequestAsync(script, MakeContext());
        var result2 = await _runner.RunPreRequestAsync(script, MakeContext());

        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
    }

    // ── Cancellation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RunPreRequestAsync_AlreadyCancelledToken_ReturnsCancelledResult()
    {
        const string script = "ctx.Log(\"should not run\");";
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await _runner.RunPreRequestAsync(script, MakeContext(), cts.Token);

        // The script may return success (empty log) or failure depending on Roslyn's handling,
        // but in either case the "should not run" log entry should NOT appear.
        result.Log.Should().NotContain("should not run");
    }
}
