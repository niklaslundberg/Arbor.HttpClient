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
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        using var sourceReader = new StreamReader(
            stream,
            System.Text.Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);
        var sourceText = sourceReader.ReadToEnd();

        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        var explicitlyEmptySecurityOperations = GetExplicitlyEmptySecurityOperations(sourceText);

        var reader = new OpenApiStreamReader();
        using var sourceTextStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(sourceText));
        var document = reader.Read(sourceTextStream, out var diagnostic);

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
        var collectionHeaders = BuildSecurityHeaders(document, document.SecurityRequirements ?? []);

        var requests = new List<CollectionRequest>();

        foreach (var path in document.Paths ?? [])
        {
            foreach (var operation in path.Value.Operations)
            {
                var method = operation.Key.ToString().ToUpperInvariant();
                var operationValue = operation.Value;
                var operationKey = CreateOperationKey(path.Key, method);

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

                var headers = BuildHeaders(
                    document,
                    operationValue,
                    effectiveParams,
                    collectionHeaders,
                    explicitlyEmptySecurityOperations.Contains(operationKey));

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

        return new Collection(0, name, sourcePath, baseUrl, requests, collectionHeaders.Count > 0 ? collectionHeaders : null);
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
        IReadOnlyList<OpenApiParameter> effectiveParams,
        IReadOnlyList<RequestHeader> collectionSecurityHeaders,
        bool operationSecurityIsExplicitlyEmpty)
    {
        var headers = new List<RequestHeader>();

        // Header parameters → {{paramName}} placeholder
        foreach (var param in effectiveParams.Where(p => p.In == ParameterLocation.Header))
        {
            headers.Add(new RequestHeader(param.Name, $"{{{{{param.Name}}}}}"));
        }

        var operationSecurityHeaders = BuildSecurityHeaders(document, operation.Security ?? []);
        foreach (var securityHeader in operationSecurityHeaders
                     .Where(securityHeader => !headers.Any(h => string.Equals(h.Name, securityHeader.Name, StringComparison.OrdinalIgnoreCase))))
        {
            headers.Add(securityHeader);
        }

        if (operationSecurityIsExplicitlyEmpty)
        {
            AddDisabledSecurityOptOutHeaders(headers, collectionSecurityHeaders);
        }
        else if (operation.Security is { Count: > 0 })
        {
            var operationSecurityHeaderNames = operationSecurityHeaders
                .Select(header => header.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var inheritedHeadersToDisable = collectionSecurityHeaders
                .Where(header => !operationSecurityHeaderNames.Contains(header.Name))
                .ToList();

            AddDisabledSecurityOptOutHeaders(headers, inheritedHeadersToDisable);
        }

        return headers;
    }

    private static void AddDisabledSecurityOptOutHeaders(
        List<RequestHeader> requestHeaders,
        IReadOnlyList<RequestHeader> inheritedSecurityHeaders)
    {
        foreach (var inheritedHeader in inheritedSecurityHeaders
                     .Where(header => !requestHeaders.Any(existing => string.Equals(existing.Name, header.Name, StringComparison.OrdinalIgnoreCase))))
        {
            requestHeaders.Add(new RequestHeader(inheritedHeader.Name, inheritedHeader.Value, IsEnabled: false));
        }
    }

    private static HashSet<string> GetExplicitlyEmptySecurityOperations(string sourceText)
    {
        var operations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var json = JsonDocument.Parse(sourceText);
            if (!json.RootElement.TryGetProperty("paths", out var paths) || paths.ValueKind != JsonValueKind.Object)
            {
                return operations;
            }

            foreach (var pathEntry in paths.EnumerateObject())
            {
                if (pathEntry.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var operationEntry in pathEntry.Value.EnumerateObject())
                {
                    if (!IsHttpOperationName(operationEntry.Name) || operationEntry.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (operationEntry.Value.TryGetProperty("security", out var security)
                        && security.ValueKind == JsonValueKind.Array
                        && security.GetArrayLength() == 0)
                    {
                        operations.Add(CreateOperationKey(pathEntry.Name, operationEntry.Name));
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Non-JSON (e.g., YAML) — explicit-empty operation security detection not available.
        }

        return operations;
    }

    private static bool IsHttpOperationName(string operationName) =>
        operationName is "get" or "put" or "post" or "delete" or "options" or "head" or "patch" or "trace";

    private static string CreateOperationKey(string path, string method) =>
        $"{method.ToUpperInvariant()} {path}";

    private static List<RequestHeader> BuildSecurityHeaders(
        OpenApiDocument document,
        IEnumerable<OpenApiSecurityRequirement> requirements)
    {
        var headers = new List<RequestHeader>();
        foreach (var requirement in requirements)
        {
            foreach (var scheme in requirement.Keys)
            {
                var schemeId = scheme.Reference?.Id ?? scheme.Name;
                var resolved = !string.IsNullOrEmpty(schemeId) &&
                               document.Components?.SecuritySchemes?.TryGetValue(schemeId, out var securityScheme) == true
                    ? securityScheme
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
                else if (resolved is { Type: SecuritySchemeType.ApiKey, In: ParameterLocation.Header }
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
            OpenApiBoolean b => b.Value,
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
