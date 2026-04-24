using System.Collections.ObjectModel;
using System.Net.Http;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace Arbor.HttpClient.Desktop.ViewModels;

/// <summary>
/// Owns the GraphQL request editor state: query text, variables JSON, operation name,
/// and schema introspection.  The parent view model calls <see cref="BuildDraft"/> to
/// obtain the fully resolved draft before sending.
/// </summary>
public sealed partial class GraphQlViewModel : ViewModelBase
{
    private readonly GraphQlService _service;
    private readonly ILogger _logger;

    [ObservableProperty]
    private string _query = "{\n  __typename\n}";

    [ObservableProperty]
    private string _variablesJson = "{}";

    [ObservableProperty]
    private string _operationName = string.Empty;

    [ObservableProperty]
    private string _schemaJson = string.Empty;

    [ObservableProperty]
    private bool _isIntrospecting;

    [ObservableProperty]
    private string _introspectionError = string.Empty;

    public GraphQlViewModel(global::System.Net.Http.HttpClient httpClient, ILogger logger)
    {
        _service = new GraphQlService(httpClient);
        _logger = logger.ForContext<GraphQlViewModel>();
    }

    /// <summary>Builds a <see cref="GraphQlDraft"/> from the current editor state.</summary>
    public GraphQlDraft BuildDraft(
        string url,
        IReadOnlyList<RequestHeader>? headers = null)
    {
        return new GraphQlDraft(url, Query, VariablesJson, OperationName, headers);
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
    [RelayCommand]
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
