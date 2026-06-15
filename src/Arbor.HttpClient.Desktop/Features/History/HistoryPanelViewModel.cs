using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.Messaging;
using Arbor.HttpClient.Desktop.Shared;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Serilog;

namespace Arbor.HttpClient.Desktop.Features.History;

/// <summary>
/// Owns the History tab: the request-history list, its search-query filter, and the commands to
/// reload history and load a history entry into the active request editor. Loading an entry
/// publishes <see cref="HistoryRequestLoadRequested"/> on the <see cref="IMessageBus"/> rather than
/// mutating the editor directly, so the History panel has no dependency on the request panel.
/// </summary>
public sealed partial class HistoryPanelViewModel : ReactiveViewModelBase
{
    private readonly RequestHistoryWorkflow _workflow;
    private readonly IMessageBus _messageBus;
    private readonly ILogger _logger;
    private readonly Subject<string> _filterRequestedSubject = new();

    [Reactive]
    private string _historySearchQuery = string.Empty;

    public HistoryPanelViewModel(
        IRequestHistoryRepository requestHistoryRepository,
        IMessageBus messageBus,
        ILogger? logger = null)
    {
        _workflow = new RequestHistoryWorkflow(requestHistoryRepository);
        _messageBus = messageBus;
        _logger = (logger ?? Log.Logger).ForContext<HistoryPanelViewModel>();

        LoadHistoryCommand = ReactiveCommand.CreateFromTask(ReloadAsync);
        LoadHistoryCommand.ThrownExceptions
            .Subscribe(exception => _logger.Error(exception, "Loading history failed unexpectedly"))
            .DisposeWith(Disposables);

        // Throttle the search box so typing doesn't re-filter on every keystroke.
        _filterRequestedSubject
            .Throttle(TimeSpan.FromMilliseconds(150))
            .DistinctUntilChanged(StringComparer.Ordinal)
            .Subscribe(query => _workflow.ApplyFilter(query))
            .DisposeWith(Disposables);

        PropertyChangedObservable
            .Select(args => args.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(HistorySearchQuery), StringComparison.Ordinal))
            .Subscribe(_ => _filterRequestedSubject.OnNext(HistorySearchQuery))
            .DisposeWith(Disposables);
    }

    /// <summary>Filtered history entries bound by the History tab.</summary>
    public ObservableCollection<RequestHistoryEntry> History => _workflow.History;

    /// <summary>Reloads recent history and re-applies the current search filter.</summary>
    public ReactiveCommand<Unit, Unit> LoadHistoryCommand { get; }

    /// <summary>Reloads recent history from the repository, re-applying the current search filter.</summary>
    public Task ReloadAsync(CancellationToken cancellationToken = default) =>
        _workflow.LoadAsync(HistorySearchQuery, cancellationToken);

    [ReactiveCommand]
    private void LoadHistoryRequest(RequestHistoryEntry? request)
    {
        if (request is null)
        {
            return;
        }

        _messageBus.Publish(new HistoryRequestLoadRequested(request));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _filterRequestedSubject.OnCompleted();
        }

        base.Dispose(disposing);
    }
}
