namespace Arbor.HttpClient.Desktop.Models;

public sealed class ApplicationOptions
{
    public HttpOptions Http { get; init; } = new();

    public AppearanceOptions Appearance { get; init; } = new();

    public ScheduledJobsOptions ScheduledJobs { get; init; } = new();

    public LayoutOptions Layouts { get; init; } = new();
}

public sealed class HttpOptions
{
    public string HttpVersion { get; init; } = "1.1";

    public string TlsVersion { get; init; } = "SystemDefault";

    public string DefaultContentType { get; init; } = "application/json";

    public bool FollowRedirects { get; init; } = true;

    public string DefaultRequestUrl { get; init; } = "https://postman-echo.com/get?hello=world";
}

public sealed class AppearanceOptions
{
    public string Theme { get; init; } = "System";

    public double FontSize { get; init; } = 13d;

    public string FontFamily { get; init; } = "Cascadia Code,Consolas,Menlo,monospace";
}

public sealed class ScheduledJobsOptions
{
    public bool AutoStartOnLaunch { get; init; } = true;

    public int DefaultIntervalSeconds { get; init; } = 60;
}

public sealed class LayoutOptions
{
    public DockLayoutSnapshot? CurrentLayout { get; init; }

    public List<NamedDockLayout> SavedLayouts { get; init; } = [];
}

public sealed class NamedDockLayout
{
    public string Name { get; init; } = string.Empty;

    public DockLayoutSnapshot Layout { get; init; } = new();
}

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

public sealed class FloatingWindowSnapshot
{
    public double X { get; init; }

    public double Y { get; init; }

    public double Width { get; init; } = 300;

    public double Height { get; init; } = 400;

    public List<string> DockableIds { get; init; } = [];

    public string? ActiveDockableId { get; init; }
}
