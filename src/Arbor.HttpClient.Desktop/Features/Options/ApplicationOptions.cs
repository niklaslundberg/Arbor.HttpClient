using Arbor.HttpClient.Desktop.Features.Layout;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
namespace Arbor.HttpClient.Desktop.Features.Options;

public sealed class ApplicationOptions
{
    public HttpOptions Http { get; init; } = new();

    public AppearanceOptions Appearance { get; init; } = new();

    public ScheduledJobsOptions ScheduledJobs { get; init; } = new();

    public LayoutOptions Layouts { get; init; } = new();
}
