namespace Arbor.HttpClient.Core.Scripting;

/// <summary>
/// Executes pre/post-request scripts and returns a <see cref="ScriptResult"/> describing
/// the outcome. The scripting engine is abstracted so implementations can be swapped
/// (e.g. Roslyn in production, a no-op stub in tests that do not exercise scripting).
/// </summary>
public interface IScriptRunner
{
    /// <summary>
    /// Executes the pre-request script against <paramref name="context"/> and returns
    /// the result. The method is a no-op (returns a successful empty result) when
    /// <paramref name="script"/> is null or whitespace.
    /// </summary>
    Task<ScriptResult> RunPreRequestAsync(
        string? script,
        ScriptContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the post-response script against <paramref name="context"/> (with
    /// <see cref="ScriptContext.Response"/> populated) and returns the result.
    /// The method is a no-op when <paramref name="script"/> is null or whitespace.
    /// </summary>
    Task<ScriptResult> RunPostResponseAsync(
        string? script,
        ScriptContext context,
        CancellationToken cancellationToken = default);
}
