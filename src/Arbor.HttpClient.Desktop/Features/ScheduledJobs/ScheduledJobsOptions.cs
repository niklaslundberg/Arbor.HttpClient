namespace Arbor.HttpClient.Desktop.Features.ScheduledJobs;

public sealed class ScheduledJobsOptions
{
    public bool AutoStartOnLaunch { get; init; } = true;

    public int DefaultIntervalSeconds { get; init; } = 60;
}
