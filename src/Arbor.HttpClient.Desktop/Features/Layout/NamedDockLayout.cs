namespace Arbor.HttpClient.Desktop.Features.Layout;

public sealed class NamedDockLayout
{
    public string Name { get; init; } = string.Empty;

    public DockLayoutSnapshot Layout { get; init; } = new();
}
