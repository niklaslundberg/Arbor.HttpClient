using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.HttpRequest;

namespace Arbor.HttpClient.Core.OpenApiImport;

public sealed class OpenApiImportService
{
    private static readonly Regex PathParamPattern = new(@"\{([^{}]+)\}", RegexOptions.Compiled);

    public Collection Import(Stream stream, string? sourcePath = null)
    {
        var reader = new OpenApiStreamReader();
        var document = reader.Read(stream, out var diagnostic);

        if (document is null)
        {
            throw new InvalidOperationException("Failed to parse OpenAPI document.");
        }

        if (diagnostic.SpecificationVersion == Microsoft.OpenApi.OpenApiSpecVersion.OpenApi2_0)
        {
            throw new NotSupportedException("OpenAPI version 2.0 (Swagger) is not supported. Only version 3.0 and higher are supported.");
        }

        var name = document.Info?.Title ?? Path.GetFileNameWithoutExtension(sourcePath) ?? "Imported Collection";
        var baseUrl = document.Servers?.Count > 0 ? document.Servers[0].Url : null;

        var requests = new List<CollectionRequest>();

        foreach (var path in document.Paths ?? [])
        {
            foreach (var operation in path.Value.Operations)
            {
                var method = operation.Key.ToString().ToUpperInvariant();
                var operationValue = operation.Value;

                var requestName = !string.IsNullOrWhiteSpace(operationValue.OperationId)
                    ? operationValue.OperationId
                    : $"{method} {path.Key}";

                // Convert OpenAPI {param} to our {{param}} convention atomically via regex
                var resolvedPath = PathParamPattern.Replace(path.Key, m => $"{{{{{m.Groups[1].Value}}}}}");

                // Merge path-item level parameters with operation-level parameters (operation takes precedence)
                var effectiveParams = MergeParameters(path.Value.Parameters, operationValue.Parameters);

                // Append query parameters as {{paramName}} placeholders
                var queryParams = effectiveParams
                    .Where(p => p.In == ParameterLocation.Query)
                    .Select(p => $"{p.Name}={{{{{p.Name}}}}}");
                var queryString = string.Join("&", queryParams);
                if (!string.IsNullOrEmpty(queryString))
                {
                    resolvedPath += "?" + queryString;
                }

                // Collect header parameters and security (auth) headers
                var headers = BuildHeaders(document, operationValue, effectiveParams);

                // Extract first example body and matching content type
                var (body, contentType) = ExtractBodyAndContentType(operationValue);

                // Use the first tag if available for tree grouping
                var tag = operationValue.Tags?.Count > 0 ? operationValue.Tags[0].Name : null;

                requests.Add(new CollectionRequest(
                    requestName,
                    method,
                    resolvedPath,
                    operationValue.Summary,
                    Tag: tag,
                    Body: body,
                    ContentType: contentType,
                    Headers: headers.Count > 0 ? headers : null));
            }
        }

        return new Collection(0, name, sourcePath, baseUrl, requests);
    }

    /// <summary>
    /// Merges path-item-level parameters with operation-level parameters.
    /// Operation-level parameters override path-item parameters with the same name+location.
    /// </summary>
    private static IReadOnlyList<OpenApiParameter> MergeParameters(
        IList<OpenApiParameter>? pathItemParams,
        IList<OpenApiParameter>? operationParams)
    {
        if (pathItemParams is null or { Count: 0 })
        {
            return operationParams?.ToList() ?? [];
        }

        if (operationParams is null or { Count: 0 })
        {
            return pathItemParams.ToList();
        }

        var merged = new Dictionary<string, OpenApiParameter>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in pathItemParams)
        {
            merged[$"{p.In}:{p.Name}"] = p;
        }

        foreach (var p in operationParams)
        {
            merged[$"{p.In}:{p.Name}"] = p;
        }

        return merged.Values.ToList();
    }

    private static List<RequestHeader> BuildHeaders(
        OpenApiDocument document,
        OpenApiOperation operation,
        IReadOnlyList<OpenApiParameter> effectiveParams)
    {
        var headers = new List<RequestHeader>();

        // Header parameters → {{paramName}} placeholder
        foreach (var param in effectiveParams.Where(p => p.In == ParameterLocation.Header))
        {
            headers.Add(new RequestHeader(param.Name, $"{{{{{param.Name}}}}}"));
        }

        // Security / auth headers derived from the effective security requirements.
        // When the operation defines no security entries (null or empty list), fall back
        // to the document-level security requirements. The Microsoft.OpenApi library
        // returns an empty (non-null) list for both "not declared" and "security: []",
        // so we treat count == 0 as "use document defaults" — this matches the common
        // case of inheriting global auth from the document root.
        IEnumerable<OpenApiSecurityRequirement> effectiveSecurityRequirements =
            operation.Security is null or { Count: 0 }
                ? document.SecurityRequirements ?? []
                : operation.Security;

        foreach (var requirement in effectiveSecurityRequirements)
        {
            foreach (var scheme in requirement.Keys)
            {
                // The key may be a reference object; resolve via document components
                var schemeId = scheme.Reference?.Id ?? scheme.Name;
                var resolved = !string.IsNullOrEmpty(schemeId) &&
                               document.Components?.SecuritySchemes?.TryGetValue(schemeId, out var s) == true
                    ? s
                    : scheme;

                if (resolved.Type == SecuritySchemeType.Http)
                {
                    if (string.Equals(resolved.Scheme, "bearer", StringComparison.OrdinalIgnoreCase)
                        && !headers.Any(h => string.Equals(h.Name, "Authorization", StringComparison.OrdinalIgnoreCase)))
                    {
                        headers.Add(new RequestHeader("Authorization", "Bearer {{bearerToken}}"));
                    }
                    else if (string.Equals(resolved.Scheme, "basic", StringComparison.OrdinalIgnoreCase)
                             && !headers.Any(h => string.Equals(h.Name, "Authorization", StringComparison.OrdinalIgnoreCase)))
                    {
                        headers.Add(new RequestHeader("Authorization", "Basic {{credentials}}"));
                    }
                }
                else if (resolved.Type == SecuritySchemeType.ApiKey && resolved.In == ParameterLocation.Header
                         && !string.IsNullOrEmpty(resolved.Name)
                         && !headers.Any(h => string.Equals(h.Name, resolved.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    headers.Add(new RequestHeader(resolved.Name, $"{{{{{resolved.Name}}}}}"));
                }
            }
        }

        return headers;
    }

    private static (string? Body, string? ContentType) ExtractBodyAndContentType(OpenApiOperation operation)
    {
        if (operation.RequestBody is null || operation.RequestBody.Content.Count == 0)
        {
            return (null, null);
        }

        // Prefer JSON, then fall back to the first available media type
        var contentEntry = operation.RequestBody.Content
            .FirstOrDefault(c => c.Key.Contains("json", StringComparison.OrdinalIgnoreCase));

        if (contentEntry.Value is null)
        {
            contentEntry = operation.RequestBody.Content.First();
        }

        var mediaType = contentEntry.Value;
        var contentType = contentEntry.Key;

        if (mediaType is null)
        {
            return (null, contentType);
        }

        // Use the inline example first; fall back to the first named example
        IOpenApiAny? exampleValue = mediaType.Example;
        if (exampleValue is null && mediaType.Examples?.Count > 0)
        {
            exampleValue = mediaType.Examples.Values.First()?.Value;
        }

        var body = exampleValue is { } ? SerializeAny(exampleValue) : null;
        return (body, contentType);
    }

    /// <summary>
    /// Converts an <see cref="IOpenApiAny"/> value to a compact JSON string using
    /// <see cref="JsonSerializer"/> so that no private OpenAPI writer APIs are needed.
    /// </summary>
    private static string? SerializeAny(IOpenApiAny any)
    {
        var obj = ConvertAny(any);
        return obj is null ? null : JsonSerializer.Serialize(obj);
    }

    private static object? ConvertAny(IOpenApiAny any)
    {
        return any switch
        {
            OpenApiNull => null,
            OpenApiBoolean b => (object)b.Value,
            OpenApiInteger i => i.Value,
            OpenApiLong l => l.Value,
            OpenApiFloat f => f.Value,
            OpenApiDouble d => d.Value,
            OpenApiString s => s.Value,
            OpenApiArray arr => arr.Select(ConvertAny).ToList(),
            OpenApiObject obj => obj.ToDictionary(kvp => kvp.Key, kvp => ConvertAny(kvp.Value)),
            _ => null
        };
    }
}
