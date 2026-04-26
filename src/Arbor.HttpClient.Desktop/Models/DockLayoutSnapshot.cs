namespace Arbor.HttpClient.Desktop.Models;

public sealed class DockLayoutSnapshot
{
    public double LeftToolProportion { get; init; } = 0.25;

    public double DocumentProportion { get; init; } = 0.75;

    public string? ActiveToolDockableId { get; init; }

    public string? ActiveDocumentDockableId { get; init; }

    public List<string> LeftToolDockableOrder { get; init; } = [];

    public List<string> DocumentDockableOrder { get; init; } = [];

    public List<FloatingWindowSnapshot> FloatingWindows { get; init; } = [];
}
