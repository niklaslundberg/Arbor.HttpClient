using System;
using System.Collections.Generic;
using System.Linq;
using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.Variables;
using Arbor.HttpClient.Desktop.Features.HttpRequest;

namespace Arbor.HttpClient.Desktop.Features.Collections;

/// <summary>One merged header to be applied to the request editor, with its inherited/manual provenance.</summary>
public sealed record CollectionRequestHeaderProjection(string Name, string Value, bool IsEnabled, bool IsInherited);

/// <summary>
/// Result of projecting a <see cref="CollectionItemViewModel"/> onto the request editor.
/// Built by <see cref="CollectionRequestEditorProjectionWorkflow"/> and applied by
/// <c>MainWindowViewModel.ApplyCollectionRequestToEditor</c>.
/// </summary>
public sealed record CollectionRequestEditorProjection(
    RequestType RequestType,
    string ResolvedUrl,
    string Name,
    string Notes,
    IReadOnlyList<CollectionRequestHeaderProjection> Headers,
    string SelectedContentTypeOption,
    string CustomContentType,
    string Body,
    bool ShowDemoServerBanner);

/// <summary>
/// Builds the editor projection for loading a collection request: resolves the request type,
/// the (possibly environment-resolved) URL, merged inherited/manual headers, the content-type
/// selection, the body, and whether the demo-server banner should be shown.
/// </summary>
public sealed class CollectionRequestEditorProjectionWorkflow(VariableResolver variableResolver)
{
    public CollectionRequestEditorProjection BuildProjection(
        CollectionItemViewModel item,
        Collection? selectedCollection,
        RequestEnvironment? activeEnvironment,
        IReadOnlyList<EnvironmentVariable> resolvedVariables,
        IReadOnlyList<string> contentTypeOptions,
        bool hasDemoServer,
        bool isDemoServerRunning,
        int demoServerPort,
        int demoServerHttpsPort)
    {
        var requestType = ResolveRequestType(item.Method);
        var resolvedUrl = BuildRequestUrl(selectedCollection?.BaseUrl, item.Path, requestType, activeEnvironment, resolvedVariables);
        var headers = BuildHeaders(item, selectedCollection?.Headers);
        var (selectedContentTypeOption, customContentType, body) = BuildContentProjection(item, contentTypeOptions);
        var showDemoServerBanner = ShouldShowDemoServerBanner(resolvedUrl, hasDemoServer, isDemoServerRunning, demoServerPort, demoServerHttpsPort);

        return new CollectionRequestEditorProjection(
            requestType,
            resolvedUrl,
            item.Name,
            item.Notes ?? string.Empty,
            headers,
            selectedContentTypeOption,
            customContentType,
            body,
            showDemoServerBanner);
    }

    public static RequestType ResolveRequestType(string method) =>
        method switch
        {
            "WS" or "WSS" => RequestType.WebSocket,
            "SSE" => RequestType.Sse,
            _ => RequestType.Http
        };

    private string BuildRequestUrl(
        string? collectionBaseUrl,
        string path,
        RequestType requestType,
        RequestEnvironment? activeEnvironment,
        IReadOnlyList<EnvironmentVariable> resolvedVariables)
    {
        var baseUrl = activeEnvironment is { }
            ? variableResolver.Resolve(collectionBaseUrl ?? string.Empty, resolvedVariables)
            : (collectionBaseUrl ?? string.Empty);

        var resolvedUrl = CollectionUrlHelper.BuildFullUrl(baseUrl, path);
        if (requestType == RequestType.WebSocket)
        {
            resolvedUrl = resolvedUrl
                .Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase)
                .Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase);
        }

        return resolvedUrl;
    }

    private static IReadOnlyList<CollectionRequestHeaderProjection> BuildHeaders(
        CollectionItemViewModel item,
        IReadOnlyList<RequestHeader>? inheritedHeaders)
    {
        var mergedHeaders = CollectionInheritedHeadersWorkflow.MergeCollectionAndRequestHeaders(inheritedHeaders, item.Headers);
        if (mergedHeaders is null)
        {
            return [];
        }

        return mergedHeaders
            .Select(mergedHeader => new CollectionRequestHeaderProjection(
                mergedHeader.Name,
                mergedHeader.Value,
                mergedHeader.IsEnabled,
                IsInheritedHeader(mergedHeader, inheritedHeaders)))
            .ToList();
    }

    private static bool IsInheritedHeader(RequestHeader header, IReadOnlyList<RequestHeader>? inheritedHeaders) =>
        inheritedHeaders?.Any(inheritedHeader =>
            string.Equals(inheritedHeader.Name, header.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(inheritedHeader.Value, header.Value, StringComparison.Ordinal)
            && inheritedHeader.IsEnabled == header.IsEnabled) == true;

    private static (string SelectedContentTypeOption, string CustomContentType, string Body) BuildContentProjection(
        CollectionItemViewModel item,
        IReadOnlyList<string> contentTypeOptions)
    {
        string selectedContentTypeOption;
        string customContentType;

        if (string.IsNullOrEmpty(item.ContentType))
        {
            selectedContentTypeOption = RequestEditorViewModel.NoneContentTypeOption;
            customContentType = string.Empty;
        }
        else if (contentTypeOptions.Contains(item.ContentType))
        {
            selectedContentTypeOption = item.ContentType;
            customContentType = string.Empty;
        }
        else
        {
            selectedContentTypeOption = RequestEditorViewModel.CustomContentTypeOption;
            customContentType = item.ContentType;
        }

        string body;
        if (!string.IsNullOrEmpty(item.Body))
        {
            body = item.Body;
        }
        else if (item.Method is "POST" or "PUT" or "PATCH")
        {
            body = "{}";
        }
        else
        {
            body = string.Empty;
        }

        return (selectedContentTypeOption, customContentType, body);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="resolvedUrl"/> targets the configured
    /// demo server (by port) and that server is not currently running.
    /// </summary>
    public static bool ShouldShowDemoServerBanner(
        string resolvedUrl,
        bool hasDemoServer,
        bool isDemoServerRunning,
        int demoServerPort,
        int demoServerHttpsPort) =>
        hasDemoServer
        && !isDemoServerRunning
        && (IsDemoServerUrl(resolvedUrl, demoServerPort) || IsDemoServerUrl(resolvedUrl, demoServerHttpsPort));

    private static bool IsDemoServerUrl(string url, int port) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Host, "127.0.0.1", StringComparison.Ordinal))
        && uri.Port == port;
}
