using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Arbor.HttpClient.Core.ScheduledJobs;
using Arbor.HttpClient.Desktop.Shared;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Serilog;

namespace Arbor.HttpClient.Desktop.Features.ScheduledJobs;

/// <summary>
/// Owns the Scheduled Jobs tab: the job list and the add/remove commands, backed by
/// <see cref="ScheduledJobsWorkflow"/>. Configuration values that live in the application options
/// (default interval, follow-redirects, auto-start) are supplied as delegates so the panel reads
/// the current value at action time without depending on the options view model.
/// </summary>
public sealed partial class ScheduledJobsPanelViewModel : ReactiveViewModelBase
{
    private readonly ScheduledJobsWorkflow _workflow;
    private readonly Func<int> _defaultIntervalSeconds;
    private readonly Func<bool> _followRedirects;
    private readonly Func<bool> _autoStartOnLaunch;
    private readonly Action? _onJobAdded;
    private readonly ILogger _logger;

    public ScheduledJobsPanelViewModel(
        IScheduledJobRepository repository,
        ScheduledJobService jobService,
        Func<int> defaultIntervalSeconds,
        Func<bool> followRedirects,
        Func<bool> autoStartOnLaunch,
        Action? onJobAdded = null,
        ILogger? logger = null)
    {
        _workflow = new ScheduledJobsWorkflow(repository, jobService);
        _defaultIntervalSeconds = defaultIntervalSeconds;
        _followRedirects = followRedirects;
        _autoStartOnLaunch = autoStartOnLaunch;
        _onJobAdded = onJobAdded;
        _logger = (logger ?? Log.Logger).ForContext<ScheduledJobsPanelViewModel>();

        RemoveJobCommand.ThrownExceptions
            .Subscribe(exception => _logger.Error(exception, "Removing scheduled job failed unexpectedly"))
            .DisposeWith(Disposables);
    }

    /// <summary>The scheduled jobs bound by the Scheduled Jobs tab.</summary>
    public ObservableCollection<ScheduledJobViewModel> Jobs => _workflow.Jobs;

    /// <summary>Loads persisted jobs, honouring the auto-start and follow-redirects options.</summary>
    public Task LoadAsync(CancellationToken cancellationToken = default) =>
        _workflow.LoadJobsAsync(_autoStartOnLaunch(), _followRedirects(), cancellationToken);

    [ReactiveCommand]
    private void AddJob()
    {
        _workflow.AddJob(_defaultIntervalSeconds(), _followRedirects());
        _onJobAdded?.Invoke();
    }

    [ReactiveCommand]
    private Task RemoveJob(ScheduledJobViewModel? job) => _workflow.RemoveJobAsync(job);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _workflow.Dispose();
        }

        base.Dispose(disposing);
    }
}
