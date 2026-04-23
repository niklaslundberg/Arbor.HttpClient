using Arbor.HttpClient.Core.Models;

namespace Arbor.HttpClient.Desktop.ViewModels;

public sealed class CollectionItemViewModel(CollectionRequest request)
{
    public string Name { get; } = request.Name;
    public string Method { get; } = request.Method;
    public string Path { get; } = request.Path;
    public string? Description { get; } = request.Description;
    public string? Notes { get; } = request.Notes;
    public CollectionRequest Request { get; } = request;
}
