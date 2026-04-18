using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arbor.HttpClient.Desktop.ViewModels;

public sealed partial class ScheduledJobViewModel : ViewModelBase
{
    private readonly IScheduledJobRepository _repository;
    private readonly ScheduledJobService _jobService;

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
        ScheduledJobService jobService)
    {
        var vm = new ScheduledJobViewModel(repository, jobService)
        {
            Id = config.Id,
            Name = config.Name,
            Method = config.Method,
            Url = config.Url,
            Body = config.Body ?? string.Empty,
            IntervalSeconds = config.IntervalSeconds,
            AutoStart = config.AutoStart
        };
        vm.IsRunning = jobService.IsRunning(config.Id);
        return vm;
    }

    public ScheduledJobConfig ToConfig() =>
        new(Id, Name, Method, Url,
            string.IsNullOrWhiteSpace(Body) ? null : Body,
            null, // headers not yet supported in the scheduled-job editor
            Math.Max(MainWindowViewModel.MinScheduledJobIntervalSeconds, IntervalSeconds),
            AutoStart);

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
        var config = ToConfig();
        if (Id == 0)
        {
            Id = await _repository.SaveAsync(config);
        }
        else
        {
            await _repository.UpdateAsync(config with { Id = Id });
        }
    }
}
