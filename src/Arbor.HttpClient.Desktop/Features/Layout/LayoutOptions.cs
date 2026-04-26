namespace Arbor.HttpClient.Desktop.Features.Layout;

public sealed class LayoutOptions
{
    public DockLayoutSnapshot? CurrentLayout { get; init; }

    public List<NamedDockLayout> SavedLayouts { get; init; } = [];
}
