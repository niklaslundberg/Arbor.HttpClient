using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Desktop.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arbor.HttpClient.Desktop.ViewModels;

public sealed partial class ScheduledJobViewModel : ViewModelBase
{
    private readonly IScheduledJobRepository _repository;
    private readonly ScheduledJobService _jobService;
    private bool _suppressAutoSave;
    private bool _isSaving;
    private CancellationTokenSource? _autoSaveCancellationTokenSource;

    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _method = "GET";

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string _body = string.Empty;

    [ObservableProperty]
    private int _intervalSeconds = 60;

    [ObservableProperty]
    private bool _autoStart;

    [ObservableProperty]
    private bool _followRedirects = true;

    [ObservableProperty]
    private bool _isRunning;

    public IReadOnlyList<string> Methods { get; } = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    public ScheduledJobViewModel(IScheduledJobRepository repository, ScheduledJobService jobService)
    {
        _repository = repository;
        _jobService = jobService;
    }

    public static ScheduledJobViewModel FromConfig(
        ScheduledJobConfig config,
        IScheduledJobRepository repository,
        ScheduledJobService jobService,
        bool defaultFollowRedirects)
    {
        var vm = new ScheduledJobViewModel(repository, jobService);
        vm._suppressAutoSave = true;
        vm.Id = config.Id;
        vm.Name = config.Name;
        vm.Method = config.Method;
        vm.Url = config.Url;
        vm.Body = config.Body ?? string.Empty;
        vm.IntervalSeconds = config.IntervalSeconds;
        vm.AutoStart = config.AutoStart;
        vm.FollowRedirects = config.FollowRedirects ?? defaultFollowRedirects;
        vm._suppressAutoSave = false;
        vm.IsRunning = jobService.IsRunning(config.Id);
        return vm;
    }

    public ScheduledJobConfig ToConfig() =>
        new(Id, Name, Method, Url,
            string.IsNullOrWhiteSpace(Body) ? null : Body,
            null, // headers not yet supported in the scheduled-job editor
            Math.Max(MainWindowViewModel.MinScheduledJobIntervalSeconds, IntervalSeconds),
            AutoStart,
            FollowRedirects);

    [RelayCommand]
    private void Start()
    {
        _jobService.Start(ToConfig());
        IsRunning = true;
    }

    [RelayCommand]
    private void Stop()
    {
        _jobService.Stop(Id);
        IsRunning = false;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_isSaving)
        {
            return;
        }

        _isSaving = true;
        var config = ToConfig();
        try
        {
            if (Id == 0)
            {
                Id = await _repository.SaveAsync(config);
            }
            else
            {
                await _repository.UpdateAsync(config with { Id = Id });
            }
        }
        finally
        {
            _isSaving = false;
        }
    }

    partial void OnNameChanged(string value) => QueueAutoSave();

    partial void OnMethodChanged(string value) => QueueAutoSave();

    partial void OnUrlChanged(string value) => QueueAutoSave();

    partial void OnBodyChanged(string value) => QueueAutoSave();

    partial void OnIntervalSecondsChanged(int value) => QueueAutoSave();

    partial void OnAutoStartChanged(bool value) => QueueAutoSave();

    partial void OnFollowRedirectsChanged(bool value) => QueueAutoSave();

    private void QueueAutoSave()
    {
        if (_suppressAutoSave || _isSaving || string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Url))
        {
            return;
        }

        _autoSaveCancellationTokenSource?.Cancel();
        _autoSaveCancellationTokenSource?.Dispose();
        _autoSaveCancellationTokenSource = new CancellationTokenSource();
        _ = TriggerAutoSaveAsync(_autoSaveCancellationTokenSource.Token);
    }

    private async Task TriggerAutoSaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(450), cancellationToken).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(async () => await SaveAsync());
        }
        catch (OperationCanceledException)
        {
            // Debounced auto-save was superseded by a newer edit.
        }
    }
}
