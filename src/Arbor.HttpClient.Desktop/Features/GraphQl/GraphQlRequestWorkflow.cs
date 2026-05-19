using System.Threading;
using System.Threading.Tasks;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Serilog;

namespace Arbor.HttpClient.Desktop.Features.GraphQl;

public sealed class GraphQlRequestWorkflow
{
    private readonly RequestEditorViewModel _requestEditor;
    private readonly GraphQlViewModel _graphQlViewModel;
    private readonly ILogger _httpRequestsLogger;

    public GraphQlRequestWorkflow(
        RequestEditorViewModel requestEditor,
        GraphQlViewModel graphQlViewModel,
        ILogger httpRequestsLogger)
    {
        _requestEditor = requestEditor;
        _graphQlViewModel = graphQlViewModel;
        _httpRequestsLogger = httpRequestsLogger;
    }

    public async Task<GraphQlRequestExecutionResult> SendAsync(CancellationToken cancellationToken = default)
    {
        var url = _requestEditor.GetResolvedUrl();
        var headers = _requestEditor.GetResolvedHeaders();

        _httpRequestsLogger.Information("GraphQL request started: {Url}", url);

        var response = await _graphQlViewModel.SendQueryAsync(url, headers, cancellationToken);

        _httpRequestsLogger.Information("GraphQL request completed: {StatusCode}", response.StatusCode);

        return new GraphQlRequestExecutionResult(url, headers, response);
    }
}

public sealed record GraphQlRequestExecutionResult(
    string Url,
    IReadOnlyList<RequestHeader> Headers,
    HttpResponseDetails Response);
