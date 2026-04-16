using System.Text.RegularExpressions;
using Arbor.HttpClient.Core.Models;
using Microsoft.OpenApi.Readers;

namespace Arbor.HttpClient.Core.Services;

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

                requests.Add(new CollectionRequest(
                    requestName,
                    method,
                    resolvedPath,
                    operationValue.Summary));
            }
        }

        return new Collection(0, name, sourcePath, baseUrl, requests);
    }
}
