namespace Arbor.HttpClient.Desktop.Models;

public sealed class NamedDockLayout
{
    public string Name { get; init; } = string.Empty;

    public DockLayoutSnapshot Layout { get; init; } = new();
}
