using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.HttpRequest;


namespace Arbor.HttpClient.Desktop.Features.Collections;

public sealed class CollectionItemViewModel(CollectionRequest request, string? baseUrl = null)
{
    public string Name { get; } = request.Name;
    public string Method { get; } = request.Method;
    public string Path { get; } = request.Path;
    public string? Description { get; } = request.Description;
    public string? Notes { get; } = request.Notes;
    public string? Tag { get; } = request.Tag;
    public string? Body { get; } = request.Body;
    public string? ContentType { get; } = request.ContentType;
    public IReadOnlyList<RequestHeader>? Headers { get; } = request.Headers;
    public CollectionRequest Request { get; } = request;

    /// <summary>
    /// Full URL: base URL (if any) joined with the path.
    /// Absolute paths are returned as-is.
    /// Falls back to <see cref="Path"/> when no base URL is provided.
    /// </summary>
    public string FullUrl { get; } = System.Uri.TryCreate(request.Path, System.UriKind.Absolute, out _)
        ? request.Path
        : !string.IsNullOrWhiteSpace(baseUrl)
            ? baseUrl.TrimEnd('/') + request.Path
            : request.Path;

    /// <summary>
    /// Key used to group requests in the tree view.
    /// Uses the OpenAPI tag when available; otherwise falls back to the top-level path segment.
    /// E.g. tag "pets" → "pets"; no tag + "/users/123" → "users"; "/" → "(root)".
    /// </summary>
    public string GroupKey { get; } = !string.IsNullOrWhiteSpace(request.Tag)
        ? request.Tag
        : GetGroupKey(request.Path);

    private static string GetGroupKey(string path)
    {
        var trimmed = path.TrimStart('/');
        var slashIndex = trimmed.IndexOf('/');
        var segment = slashIndex >= 0 ? trimmed[..slashIndex] : trimmed;
        return string.IsNullOrWhiteSpace(segment) ? "(root)" : segment;
    }
}
