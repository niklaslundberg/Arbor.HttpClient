using System.Collections.ObjectModel;
using Arbor.HttpClient.Core.ScheduledJobs;

namespace Arbor.HttpClient.Desktop.Features.ScheduledJobs;

/// <summary>
/// Owns the scheduled-job list lifecycle: creating new job view models, removing jobs
/// (stop, delete from the repository, dispose), and loading persisted jobs with
/// optional auto-start on launch.
/// </summary>
public sealed class ScheduledJobsWorkflow : IDisposable
{
    public const int MinIntervalSeconds = 1;

    private readonly IScheduledJobRepository _scheduledJobRepository;
    private readonly ScheduledJobService _scheduledJobService;

    public ScheduledJobsWorkflow(
        IScheduledJobRepository scheduledJobRepository,
        ScheduledJobService scheduledJobService)
    {
        _scheduledJobRepository = scheduledJobRepository;
        _scheduledJobService = scheduledJobService;
    }

    /// <summary>Job view models bound by the scheduled-jobs panel.</summary>
    public ObservableCollection<ScheduledJobViewModel> Jobs { get; } = [];

    /// <summary>
    /// Creates a new unsaved job with the given defaults and adds it to <see cref="Jobs"/>.
    /// The interval is clamped to <see cref="MinIntervalSeconds"/>.
    /// </summary>
    public ScheduledJobViewModel AddJob(int defaultIntervalSeconds, bool followRedirects)
    {
        var scheduledJobViewModel = new ScheduledJobViewModel(_scheduledJobRepository, _scheduledJobService)
        {
            IntervalSeconds = Math.Max(MinIntervalSeconds, defaultIntervalSeconds),
            FollowRedirects = followRedirects
        };
        Jobs.Add(scheduledJobViewModel);
        return scheduledJobViewModel;
    }

    /// <summary>
    /// Stops the job, deletes it from the repository when it has been persisted,
    /// removes it from <see cref="Jobs"/>, and disposes the view model.
    /// </summary>
    public async Task RemoveJobAsync(ScheduledJobViewModel? job, CancellationToken cancellationToken = default)
    {
        if (job is null)
        {
            return;
        }

        _scheduledJobService.Stop(job.Id);
        if (job.Id != 0)
        {
            await _scheduledJobRepository.DeleteAsync(job.Id, cancellationToken);
        }

        Jobs.Remove(job);
        job.Dispose();
    }

    /// <summary>
    /// Replaces <see cref="Jobs"/> with the persisted configurations, starting jobs marked
    /// for auto-start when <paramref name="autoStartOnLaunch"/> is enabled.
    /// </summary>
    public async Task LoadJobsAsync(
        bool autoStartOnLaunch,
        bool defaultFollowRedirects,
        CancellationToken cancellationToken = default)
    {
        var configs = await _scheduledJobRepository.GetAllAsync(cancellationToken);

        foreach (var scheduledJobViewModel in Jobs)
        {
            scheduledJobViewModel.Dispose();
        }

        Jobs.Clear();
        foreach (var config in configs)
        {
            var scheduledJobViewModel = ScheduledJobViewModel.FromConfig(
                config, _scheduledJobRepository, _scheduledJobService, defaultFollowRedirects);
            Jobs.Add(scheduledJobViewModel);

            if (autoStartOnLaunch && config.AutoStart && !_scheduledJobService.IsRunning(config.Id))
            {
                _scheduledJobService.Start(config, scheduledJobViewModel.IsWebViewEnabled ? scheduledJobViewModel.HandleResponseAsync : null);
                scheduledJobViewModel.IsRunning = true;
            }
        }
    }

    /// <summary>Disposes the job view models. The job service is owned and disposed by the host.</summary>
    public void Dispose()
    {
        foreach (var scheduledJobViewModel in Jobs)
        {
            scheduledJobViewModel.Dispose();
        }
    }
}
