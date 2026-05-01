using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Arbor.HttpClient.Core.Scripting;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Arbor.HttpClient.Desktop.Features.Scripting;

/// <summary>
/// Executes C# scripts using Roslyn <c>CSharpScript</c>. Compiled scripts are
/// cached by the SHA-256 hash of their source text so that repeated executions
/// of the same script avoid re-compilation overhead.
///
/// Scripts receive a globals object with a single public field <c>ctx</c> of type
/// <see cref="ScriptContext"/>, so all scripts can use <c>ctx.Method</c>,
/// <c>ctx.Env["key"]</c>, <c>ctx.Log("msg")</c>, etc.
/// </summary>
public sealed class RoslynScriptRunner : IScriptRunner
{
    /// <summary>Globals object injected into every script. Exposes <c>ctx</c> as the named entry point.</summary>
    public sealed class ScriptGlobals
    {
        public ScriptContext ctx = null!;
    }

    private static readonly ScriptOptions ScriptOptions = ScriptOptions.Default
        .WithImports(
            "System",
            "System.Linq",
            "System.Collections.Generic",
            "System.Text",
            "System.Text.Json",
            "System.Net.Http",
            "Arbor.HttpClient.Core.Scripting")
        .WithReferences(
            typeof(ScriptContext).Assembly,                    // Arbor.HttpClient.Core
            typeof(object).Assembly,                            // System.Private.CoreLib
            typeof(System.Text.Json.JsonDocument).Assembly);   // System.Text.Json (BCL)

    private readonly ConcurrentDictionary<string, Script<object>> _cache = new();

    /// <inheritdoc />
    public async Task<ScriptResult> RunPreRequestAsync(
        string? script,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return await RunAsync(script, context, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ScriptResult> RunPostResponseAsync(
        string? script,
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return await RunAsync(script, context, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ScriptResult> RunAsync(
        string? script,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return ScriptResult.Ok([]);
        }

        var compiled = GetOrCompile(script, out var compilationErrors);
        if (compiled is null)
        {
            return ScriptResult.Fail(compilationErrors, []);
        }

        var globals = new ScriptGlobals { ctx = context };
        try
        {
            var state = await compiled
                .RunAsync(globals, catchException: _ => true, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (state.Exception is { } ex)
            {
                return ScriptResult.Fail([$"Runtime error: {ex.Message}"], context.GetLog().ToList());
            }

            var log = context.GetLog().ToList();
            var assertionErrors = context.GetAssertionErrors();

            if (assertionErrors.Count > 0)
            {
                return ScriptResult.Fail(assertionErrors.ToList(), log);
            }

            return ScriptResult.Ok(log);
        }
        catch (OperationCanceledException)
        {
            return ScriptResult.Fail(["Script execution was cancelled."], context.GetLog().ToList());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return ScriptResult.Fail([$"Runtime error: {ex.Message}"], context.GetLog().ToList());
        }
    }

    private Script<object>? GetOrCompile(string source, out IReadOnlyList<string> errors)
    {
        var hash = ComputeHash(source);

        if (_cache.TryGetValue(hash, out var cached))
        {
            errors = [];
            return cached;
        }

        var script = CSharpScript.Create<object>(source, ScriptOptions, globalsType: typeof(ScriptGlobals));
        var diagnostics = script.Compile();
        var errorMessages = diagnostics
            .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .Select(d => d.ToString())
            .ToList();

        if (errorMessages.Count > 0)
        {
            errors = errorMessages;
            return null;
        }

        _cache[hash] = script;
        errors = [];
        return script;
    }

    private static string ComputeHash(string source)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return Convert.ToHexStringLower(bytes);
    }
}
