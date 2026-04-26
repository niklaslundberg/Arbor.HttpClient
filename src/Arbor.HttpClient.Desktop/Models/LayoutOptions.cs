namespace Arbor.HttpClient.Desktop.Models;

public sealed class LayoutOptions
{
    public DockLayoutSnapshot? CurrentLayout { get; init; }

    public List<NamedDockLayout> SavedLayouts { get; init; } = [];
}
