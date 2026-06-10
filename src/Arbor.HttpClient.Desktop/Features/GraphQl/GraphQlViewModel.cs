using System.Collections.ObjectModel;
using System.Net.Http;
using System.Reactive.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Serilog;
using Arbor.HttpClient.Desktop.Shared;
using Arbor.HttpClient.Core.GraphQl;
using Arbor.HttpClient.Core.HttpRequest;

namespace Arbor.HttpClient.Desktop.Features.GraphQl;

/// <summary>
/// Owns the GraphQL request editor state: query text, variables JSON, operation name,
/// and schema introspection.  The parent view model calls <see cref="BuildDraft"/> to
/// obtain the fully resolved draft before sending.
/// </summary>
public sealed partial class GraphQlViewModel : ReactiveViewModelBase
{
    private static readonly JsonSerializerOptions GraphQlJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly GraphQlService _service;
    private readonly ILogger _logger;

    [Reactive]
    private string _query = "{\n  __typename\n}";

    [Reactive]
    private string _variablesJson = "{}";

    [Reactive]
    private string _operationName = string.Empty;

    [Reactive]
    private string _schemaJson = string.Empty;

    [Reactive]
    private bool _isIntrospecting;

    [Reactive]
    private string _introspectionError = string.Empty;

    public GraphQlViewModel(System.Net.Http.HttpClient httpClient, ILogger logger)
    {
        _service = new GraphQlService(httpClient);
        _logger = logger.ForContext<GraphQlViewModel>();

        this.WhenAnyValue(
                viewModel => viewModel.Query,
                viewModel => viewModel.VariablesJson,
                viewModel => viewModel.OperationName)
            .Skip(1)
            .Subscribe(_ => ClearIntrospectionOutcome())
            .DisposeWith(Disposables);
    }

    private void ClearIntrospectionOutcome()
    {
        if (IsIntrospecting)
        {
            return;
        }

        if (!string.IsNullOrEmpty(IntrospectionError))
        {
            IntrospectionError = string.Empty;
        }
    }

    /// <summary>Builds a <see cref="GraphQlDraft"/> from the current editor state.</summary>
    public GraphQlDraft BuildDraft(
        string url,
        IReadOnlyList<RequestHeader>? headers = null)
    {
        return new GraphQlDraft(url, Query, VariablesJson, OperationName, headers);
    }

    /// <summary>Builds the serialized GraphQL request JSON payload from current editor values.</summary>
    public string BuildRequestBodyJson()
    {
        JsonNode? variables = null;
        if (!string.IsNullOrWhiteSpace(VariablesJson))
        {
            try
            {
                variables = JsonNode.Parse(VariablesJson);
            }
            catch (JsonException)
            {
                variables = null;
            }
        }

        var requestBody = new JsonObject
        {
            ["query"] = Query,
            ["variables"] = variables
        };

        if (!string.IsNullOrWhiteSpace(OperationName))
        {
            requestBody["operationName"] = OperationName;
        }

        return JsonSerializer.Serialize(requestBody, GraphQlJsonOptions);
    }

    /// <summary>
    /// Sends the current query to <paramref name="url"/> and returns the raw response details.
    /// </summary>
    /// <param name="url">The GraphQL endpoint URL (must be http:// or https://).</param>
    /// <param name="headers">Optional extra request headers to include.</param>
    /// <param name="cancellationToken">Token to cancel the in-flight request.</param>
    /// <returns>
    /// An <see cref="HttpResponseDetails"/> containing the status code, response body, headers,
    /// elapsed time, and raw response bytes.
    /// </returns>
    public Task<HttpResponseDetails> SendQueryAsync(
        string url,
        IReadOnlyList<RequestHeader>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var draft = BuildDraft(url, headers);
        return _service.SendQueryAsync(draft, cancellationToken);
    }

    /// <summary>Runs a GraphQL introspection query against the supplied URL and populates <see cref="SchemaJson"/>.</summary>
    [ReactiveCommand]
    private async Task IntrospectSchemaAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            IntrospectionError = "Enter a URL before running schema introspection.";
            return;
        }

        IsIntrospecting = true;
        IntrospectionError = string.Empty;
        SchemaJson = string.Empty;

        try
        {
            _logger.Information("GraphQL introspection started for {Url}", url);
            SchemaJson = await _service.IntrospectSchemaAsync(url).ConfigureAwait(true);
            _logger.Information("GraphQL introspection completed for {Url}", url);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "GraphQL introspection failed for {Url}", url);
            IntrospectionError = $"Introspection failed: {ex.Message}";
        }
        finally
        {
            IsIntrospecting = false;
        }
    }
}
