namespace Arbor.HttpClient.Core.Scripting;

/// <summary>
/// The outcome of executing a pre/post-request script.
/// </summary>
public sealed class ScriptResult
{
    private ScriptResult(bool success, IReadOnlyList<string> errors, IReadOnlyList<string> log)
    {
        Success = success;
        Errors = errors;
        Log = log;
    }

    /// <summary><see langword="true"/> when the script compiled and ran without errors.</summary>
    public bool Success { get; }

    /// <summary>
    /// Compilation errors, runtime exceptions, and failed assertions.
    /// Empty when <see cref="Success"/> is <see langword="true"/>.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>Messages written by the script via <see cref="ScriptContext.Log"/>.</summary>
    public IReadOnlyList<string> Log { get; }

    public static ScriptResult Ok(IReadOnlyList<string> log) =>
        new(success: true, errors: [], log: log);

    public static ScriptResult Fail(IReadOnlyList<string> errors, IReadOnlyList<string> log) =>
        new(success: false, errors: errors, log: log);
}
