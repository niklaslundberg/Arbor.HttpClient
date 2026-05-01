namespace Arbor.HttpClient.Core.Scripting;

/// <summary>
/// Globals object injected into every pre/post-request script.
/// The script receives an instance named <c>ctx</c> and can read and
/// write <see cref="Env"/> to propagate variable changes back to the host.
/// </summary>
public sealed class ScriptContext
{
    private readonly List<string> _log = [];
    private readonly List<string> _assertionErrors = [];

    public ScriptContext(
        string method,
        string url,
        IDictionary<string, string> headers,
        string? body,
        IDictionary<string, string> env)
    {
        Method = method;
        Url = url;
        Headers = headers;
        Body = body;
        Env = env;
    }

    /// <summary>HTTP method (e.g. "GET", "POST"). Pre-request scripts may modify this.</summary>
    public string Method { get; set; }

    /// <summary>Full request URL. Pre-request scripts may modify this.</summary>
    public string Url { get; set; }

    /// <summary>
    /// Request headers. Pre-request scripts may add, remove, or replace entries.
    /// The dictionary uses case-insensitive key comparison.
    /// </summary>
    public IDictionary<string, string> Headers { get; }

    /// <summary>Request body. Pre-request scripts may modify this.</summary>
    public string? Body { get; set; }

    /// <summary>
    /// Active environment variables. Scripts may read existing variables or
    /// set new values — changes are written back to the active environment after the script completes.
    /// </summary>
    public IDictionary<string, string> Env { get; }

    /// <summary>
    /// The HTTP response. Available only in post-response scripts (null in pre-request scripts).
    /// </summary>
    public ScriptResponse? Response { get; set; }

    /// <summary>Writes a message to the Script Log panel.</summary>
    public void Log(string message) =>
        _log.Add(message ?? string.Empty);

    /// <summary>
    /// Records a named assertion failure. The assertion is considered failed
    /// when <paramref name="condition"/> is <see langword="false"/>.
    /// Unlike <c>throw</c>, failed assertions are collected and shown in the log
    /// rather than stopping script execution.
    /// </summary>
    public void Assert(bool condition, string message)
    {
        if (!condition)
        {
            _assertionErrors.Add(message ?? "Assertion failed.");
        }
    }

    public IReadOnlyList<string> GetLog() => _log;
    public IReadOnlyList<string> GetAssertionErrors() => _assertionErrors;
}
