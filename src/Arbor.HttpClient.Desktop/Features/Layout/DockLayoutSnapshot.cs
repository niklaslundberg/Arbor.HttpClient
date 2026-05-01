namespace Arbor.HttpClient.Desktop.Features.Layout;

public sealed class DockLayoutSnapshot
{
    public double LeftToolProportion { get; init; } = 0.25;

    public double DocumentProportion { get; init; } = 0.75;

    /// <summary>Proportion of the request dock in the vertical request/response split (0 means use default).</summary>
    public double RequestDockProportion { get; init; }

    /// <summary>Proportion of the response dock in the vertical request/response split (0 means use default).</summary>
    public double ResponseDockProportion { get; init; }

    public string? ActiveToolDockableId { get; init; }

    public List<string> LeftToolDockableOrder { get; init; } = [];

    public List<FloatingWindowSnapshot> FloatingWindows { get; init; } = [];
}
