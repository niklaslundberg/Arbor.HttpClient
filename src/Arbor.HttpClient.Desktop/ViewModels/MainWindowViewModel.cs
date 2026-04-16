using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arbor.HttpClient.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly HttpRequestService _httpRequestService;
    private readonly IRequestHistoryRepository _requestHistoryRepository;

    [ObservableProperty]
    private string _requestName = "Sample Request";

    [ObservableProperty]
    private string _selectedMethod = "GET";

    [ObservableProperty]
    private string _requestUrl = "https://postman-echo.com/get?hello=world";

    [ObservableProperty]
    private string _requestBody = string.Empty;

    [ObservableProperty]
    private string _responseStatus = string.Empty;

    [ObservableProperty]
    private string _responseBody = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public MainWindowViewModel(HttpRequestService httpRequestService, IRequestHistoryRepository requestHistoryRepository)
    {
        _httpRequestService = httpRequestService;
        _requestHistoryRepository = requestHistoryRepository;
        Methods = ["GET", "POST", "PUT", "PATCH", "DELETE"];
        History = [];
        SendRequestCommand = new AsyncRelayCommand(SendRequestAsync);
        LoadHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync);
    }

    public IReadOnlyList<string> Methods { get; }

    public ObservableCollection<SavedRequest> History { get; }

    public IAsyncRelayCommand SendRequestCommand { get; }

    public IAsyncRelayCommand LoadHistoryCommand { get; }

    private async Task SendRequestAsync()
    {
        try
        {
            ErrorMessage = string.Empty;
            var response = await _httpRequestService.SendAsync(
                new HttpRequestDraft(RequestName, SelectedMethod, RequestUrl, RequestBody));

            ResponseStatus = $"{response.StatusCode} {response.ReasonPhrase}";
            ResponseBody = response.Body;

            await LoadHistoryAsync();
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    private async Task LoadHistoryAsync()
    {
        var requests = await _requestHistoryRepository.GetRecentAsync(20);

        History.Clear();
        foreach (var request in requests)
        {
            History.Add(request);
        }
    }
}
