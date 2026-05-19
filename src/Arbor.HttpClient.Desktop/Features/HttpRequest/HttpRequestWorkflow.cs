using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.Scripting;
using Arbor.HttpClient.Core.Variables;
using Arbor.HttpClient.Desktop.Features.Scripting;
using Arbor.HttpClient.Desktop.Localization;
using Serilog;

namespace Arbor.HttpClient.Desktop.Features.HttpRequest;

/// <summary>
/// Executes the HTTP request send pipeline (draft build, scripting pre/post hooks, transport send)
/// and returns a structured execution result for UI-layer projection.
/// </summary>
public sealed class HttpRequestWorkflow
{
    private readonly HttpRequestService _httpRequestService;
    private readonly RequestEditorViewModel _requestEditor;
    private readonly Func<IReadOnlyList<EnvironmentVariable>> _getActiveVariables;
    private readonly IScriptRunner _scriptRunner;
    private readonly ScriptViewModel _scriptViewModel;
    private readonly ILogger _httpRequestsLogger;

    public HttpRequestWorkflow(
        HttpRequestService httpRequestService,
        RequestEditorViewModel requestEditor,
        Func<IReadOnlyList<EnvironmentVariable>> getActiveVariables,
        IScriptRunner scriptRunner,
        ScriptViewModel scriptViewModel,
        ILogger httpRequestsLogger)
    {
        _httpRequestService = httpRequestService;
        _requestEditor = requestEditor;
        _getActiveVariables = getActiveVariables;
        _scriptRunner = scriptRunner;
        _scriptViewModel = scriptViewModel;
        _httpRequestsLogger = httpRequestsLogger;
    }

    public async Task<HttpRequestExecutionResult> SendAsync(CancellationToken cancellationToken)
    {
        var draft = _requestEditor.BuildResolvedHttpRequestDraft();

        _httpRequestsLogger.Information("Manual request started: {Method} {Url}", draft.Method, draft.Url);

        var resolvedHeaders = _requestEditor.GetResolvedHeaders()
            .GroupBy(header => header.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.OrdinalIgnoreCase);
        var environmentVariables = _getActiveVariables()
            .GroupBy(variable => variable.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.OrdinalIgnoreCase);

        var scriptContext = new ScriptContext(
            draft.Method,
            draft.Url,
            resolvedHeaders,
            draft.Body,
            environmentVariables);

        _scriptViewModel.ClearPreviousRun();
        var preRequestResult = await _scriptRunner.RunPreRequestAsync(_scriptViewModel.PreRequestScript, scriptContext);
        _scriptViewModel.SetResult(preRequestResult);

        if (!preRequestResult.Success)
        {
            return HttpRequestExecutionResult.PreRequestFailed(string.Join(Environment.NewLine, preRequestResult.Errors));
        }

        var mutatedHeaders = scriptContext.Headers
            .Select(pair => new RequestHeader(pair.Key, pair.Value))
            .ToList();
        var mutatedDraft = draft with
        {
            Method = scriptContext.Method,
            Url = scriptContext.Url,
            Body = scriptContext.Body,
            Headers = mutatedHeaders.Count > 0 ? mutatedHeaders : draft.Headers
        };

        if (_requestEditor.ValidateUrlBeforeSend && !IsAbsoluteHttpOrHttpsUrl(mutatedDraft.Url))
        {
            return HttpRequestExecutionResult.PreRequestFailed(Strings.RequestInvalidResolvedUrlMessage);
        }

        var response = await _httpRequestService.SendAsync(mutatedDraft, cancellationToken);

        _httpRequestsLogger.Information("Manual request completed: {StatusCode} {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);

        var responseHeaders = response.Headers
            .GroupBy(header => header.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.OrdinalIgnoreCase);
        scriptContext.Response = new ScriptResponse(
            response.StatusCode,
            response.ReasonPhrase,
            response.Body,
            responseHeaders);

        var postResponseResult = await _scriptRunner.RunPostResponseAsync(_scriptViewModel.PostResponseScript, scriptContext);
        _scriptViewModel.SetResult(postResponseResult);

        return HttpRequestExecutionResult.Success(mutatedDraft, response);
    }

    private static bool IsAbsoluteHttpOrHttpsUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https";
}

public sealed record HttpRequestExecutionResult
{
    private HttpRequestExecutionResult(bool isSuccessful, string? errorMessage, ResolvedHttpRequestDraft? requestDraft, HttpResponseDetails? response)
    {
        IsSuccessful = isSuccessful;
        ErrorMessage = errorMessage;
        RequestDraft = requestDraft;
        Response = response;
    }

    public bool IsSuccessful { get; }
    public string? ErrorMessage { get; }
    public ResolvedHttpRequestDraft? RequestDraft { get; }
    public HttpResponseDetails? Response { get; }

    public static HttpRequestExecutionResult Success(ResolvedHttpRequestDraft requestDraft, HttpResponseDetails response) =>
        new(true, null, requestDraft, response);

    public static HttpRequestExecutionResult PreRequestFailed(string errorMessage) =>
        new(false, errorMessage, null, null);
}
