using System.Text.Json;
using Arbor.HttpClient.Core.Scripting;

namespace Arbor.HttpClient.Core.Tests;

/// <summary>
/// Unit tests for <see cref="ScriptContext"/>, <see cref="ScriptResponse"/>,
/// and <see cref="ScriptResult"/> — the scripting domain types in Core.
/// These tests have no Avalonia or Roslyn dependency.
/// </summary>
public class ScriptContextTests
{
    // ── ScriptContext ──────────────────────────────────────────────────────────

    [Fact]
    public void ScriptContext_Constructor_InitializesProperties()
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Accept"] = "application/json" };
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["baseUrl"] = "https://example.com" };

        var ctx = new ScriptContext("POST", "https://example.com/api", headers, "{}", env);

        ctx.Method.Should().Be("POST");
        ctx.Url.Should().Be("https://example.com/api");
        ctx.Body.Should().Be("{}");
        ctx.Headers["Accept"].Should().Be("application/json");
        ctx.Env["baseUrl"].Should().Be("https://example.com");
        ctx.Response.Should().BeNull();
    }

    [Fact]
    public void Log_AddMessage_AppendedToLog()
    {
        var ctx = new ScriptContext("GET", "https://example.com", new Dictionary<string, string>(), null, new Dictionary<string, string>());

        ctx.Log("hello");
        ctx.Log("world");

        ctx.GetLog().Should().Equal("hello", "world");
    }

    [Fact]
    public void Assert_ConditionTrue_DoesNotAddError()
    {
        var ctx = new ScriptContext("GET", "https://example.com", new Dictionary<string, string>(), null, new Dictionary<string, string>());

        ctx.Assert(true, "should not fail");

        ctx.GetAssertionErrors().Should().BeEmpty();
    }

    [Fact]
    public void Assert_ConditionFalse_AddsError()
    {
        var ctx = new ScriptContext("GET", "https://example.com", new Dictionary<string, string>(), null, new Dictionary<string, string>());

        ctx.Assert(false, "status must be 200");

        ctx.GetAssertionErrors().Should().Equal("status must be 200");
    }

    [Fact]
    public void Assert_NullMessage_FallsBackToDefault()
    {
        var ctx = new ScriptContext("GET", "https://example.com", new Dictionary<string, string>(), null, new Dictionary<string, string>());

        ctx.Assert(false, null!);

        ctx.GetAssertionErrors().Should().ContainSingle().Which.Should().NotBeEmpty();
    }

    // ── ScriptResponse ─────────────────────────────────────────────────────────

    [Fact]
    public void ScriptResponse_ValidJson_ParsesBodyJson()
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "application/json" };
        var response = new ScriptResponse(200, "OK", """{"id":42,"name":"test"}""", headers);

        response.StatusCode.Should().Be(200);
        response.ReasonPhrase.Should().Be("OK");
        response.Body.Should().Contain("\"id\"");
        response.BodyJson.Should().NotBeNull();
        response.BodyJson!.Value.GetProperty("id").GetInt32().Should().Be(42);
        response.BodyJson!.Value.GetProperty("name").GetString().Should().Be("test");
    }

    [Fact]
    public void ScriptResponse_InvalidJson_BodyJsonIsNull()
    {
        var response = new ScriptResponse(200, "OK", "not json", new Dictionary<string, string>());

        response.BodyJson.Should().BeNull();
    }

    [Fact]
    public void ScriptResponse_EmptyBody_BodyJsonIsNull()
    {
        var response = new ScriptResponse(200, "OK", string.Empty, new Dictionary<string, string>());

        response.BodyJson.Should().BeNull();
    }

    [Fact]
    public void ScriptResponse_WhitespaceBody_BodyJsonIsNull()
    {
        var response = new ScriptResponse(204, "No Content", "   ", new Dictionary<string, string>());

        response.BodyJson.Should().BeNull();
    }

    [Fact]
    public void ScriptResponse_JsonArray_ParsesBodyJson()
    {
        var response = new ScriptResponse(200, "OK", "[1,2,3]", new Dictionary<string, string>());

        response.BodyJson.Should().NotBeNull();
        response.BodyJson!.Value.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // ── ScriptResult ───────────────────────────────────────────────────────────

    [Fact]
    public void ScriptResult_Ok_IsSuccess()
    {
        var result = ScriptResult.Ok(["entry 1"]);

        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Log.Should().Equal("entry 1");
    }

    [Fact]
    public void ScriptResult_Fail_IsNotSuccess()
    {
        var result = ScriptResult.Fail(["Compilation error"], ["log entry"]);

        result.Success.Should().BeFalse();
        result.Errors.Should().Equal("Compilation error");
        result.Log.Should().Equal("log entry");
    }
}
