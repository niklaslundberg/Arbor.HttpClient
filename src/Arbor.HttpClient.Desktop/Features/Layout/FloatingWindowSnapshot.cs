namespace Arbor.HttpClient.Desktop.Features.Layout;

public sealed class FloatingWindowSnapshot
{
    public double X { get; init; }

    public double Y { get; init; }

    public double Width { get; init; } = 300;

    public double Height { get; init; } = 400;

    public List<string> DockableIds { get; init; } = [];

    public string? ActiveDockableId { get; init; }
}
