using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Serilog;
using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Core.Environments;

namespace Arbor.HttpClient.Desktop.Features.Environments;

public sealed partial class EnvironmentsViewModel : Tool, IDisposable
{
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly RequestEditorViewModel _requestEditor;
    private readonly Func<IStorageProvider?> _getStorageProvider;
    private readonly ILogger _logger;
    private bool _suppressEnvironmentAutoSave;
    private bool _isSavingEnvironment;
    private bool _preserveFormStateOnSave;
    private CancellationTokenSource? _environmentAutoSaveCts;
    private sealed record ExportEnvironmentVariable(string Key, string Value, bool Enabled);
    private sealed record ExportEnvironment(string Name, IReadOnlyList<ExportEnvironmentVariable> Variables);
    private sealed record EnvironmentExportPayload(string Format, int Version, IReadOnlyList<ExportEnvironment> Environments);

    public EnvironmentsViewModel(
        IEnvironmentRepository environmentRepository,
        RequestEditorViewModel requestEditor,
        Func<IStorageProvider?> getStorageProvider,
        ILogger? logger = null)
    {
        _environmentRepository = environmentRepository;
        _requestEditor = requestEditor;
        _getStorageProvider = getStorageProvider;
        _logger = logger ?? Log.Logger;
        Id = "environments";
        Title = "Environments";

        Environments = [];
        ActiveEnvironmentVariables = [];

        ActiveEnvironmentVariables.CollectionChanged += OnActiveEnvironmentVariablesCollectionChanged;
    }

    public ObservableCollection<RequestEnvironment> Environments { get; }
    public ObservableCollection<EnvironmentVariableViewModel> ActiveEnvironmentVariables { get; }

    [ObservableProperty]
    private RequestEnvironment? _activeEnvironment;

    [ObservableProperty]
    private bool _isEnvironmentPanelVisible;

    [ObservableProperty]
    private string _newEnvironmentName = string.Empty;

    public async Task LoadEnvironmentsAsync(CancellationToken cancellationToken = default)
    {
        var all = await _environmentRepository.GetAllAsync(cancellationToken);

        var previousId = ActiveEnvironment?.Id;

        var previousSuppressEnvironmentAutoSave = _suppressEnvironmentAutoSave;
        _suppressEnvironmentAutoSave = true;
        try
        {
            ActiveEnvironment = null;

            Environments.Clear();
            foreach (var environment in all)
            {
                Environments.Add(environment);
            }

            if (previousId.HasValue)
            {
                ActiveEnvironment = Environments.FirstOrDefault(environment => environment.Id == previousId.Value);
            }
        }
        finally
        {
            _suppressEnvironmentAutoSave = previousSuppressEnvironmentAutoSave;
        }
    }

    /// <summary>Returns all persisted environments without modifying observable state.</summary>
    public Task<IReadOnlyList<RequestEnvironment>> GetAllEnvironmentsAsync(CancellationToken cancellationToken = default) =>
        _environmentRepository.GetAllAsync(cancellationToken);

    /// <summary>
    /// Creates a new environment with the supplied <paramref name="variables"/> and reloads
    /// the environment list.  Intended for first-run seeding only.
    /// </summary>
    public async Task SeedEnvironmentAsync(
        string name,
        IReadOnlyList<EnvironmentVariable> variables,
        CancellationToken cancellationToken = default)
    {
        await _environmentRepository.SaveAsync(name, variables, cancellationToken);
        await LoadEnvironmentsAsync(cancellationToken);
    }

    public IReadOnlyList<EnvironmentVariable> GetActiveVariablesForEditor() =>
        ActiveEnvironmentVariables
            .Select(variable => new EnvironmentVariable(variable.Name, variable.Value, variable.IsEnabled))
            .ToList();

    partial void OnNewEnvironmentNameChanged(string value) => QueueEnvironmentAutoSave();

    partial void OnActiveEnvironmentChanged(RequestEnvironment? value)
    {
        var previousSuppressEnvironmentAutoSave = _suppressEnvironmentAutoSave;
        _suppressEnvironmentAutoSave = true;
        try
        {
            if (!_preserveFormStateOnSave)
            {
                ActiveEnvironmentVariables.Clear();
                if (value is { } env)
                {
                    foreach (var variable in env.Variables)
                    {
                        ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel(variable.Name, variable.Value, variable.IsEnabled));
                    }
                }
            }
        }
        finally
        {
            _suppressEnvironmentAutoSave = previousSuppressEnvironmentAutoSave;
        }

        _requestEditor.RefreshRequestPreview();
    }

    [RelayCommand]
    private void AddEnvironmentVariable()
    {
        ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel(string.Empty, string.Empty, true));
        _logger.Information("Added environment variable placeholder");
    }

    [RelayCommand]
    private void RemoveEnvironmentVariable(EnvironmentVariableViewModel? variable)
    {
        if (variable is null)
        {
            return;
        }

        ActiveEnvironmentVariables.Remove(variable);
        _logger.Information("Removed environment variable {VariableName}", variable.Name);
    }

    [RelayCommand]
    private Task SaveEnvironmentAsync() => SaveEnvironmentCoreAsync(closeEnvironmentPanel: true, CancellationToken.None);

    [RelayCommand]
    private async Task DeleteEnvironmentAsync(RequestEnvironment? environment)
    {
        if (environment is null)
        {
            return;
        }

        await _environmentRepository.DeleteAsync(environment.Id);
        _logger.Information("Deleted environment {EnvironmentName}", environment.Name);
        if (ActiveEnvironment?.Id == environment.Id)
        {
            ActiveEnvironment = null;
        }

        await LoadEnvironmentsAsync();
    }

    [RelayCommand]
    private void EditEnvironment(RequestEnvironment? environment)
    {
        if (environment is null)
        {
            return;
        }

        var previousSuppressEnvironmentAutoSave = _suppressEnvironmentAutoSave;
        _suppressEnvironmentAutoSave = true;
        try
        {
            ActiveEnvironment = environment;
            NewEnvironmentName = environment.Name;
            _logger.Information("Editing environment {EnvironmentName}", environment.Name);
            ActiveEnvironmentVariables.Clear();
            foreach (var variable in environment.Variables)
            {
                ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel(variable.Name, variable.Value, variable.IsEnabled));
            }

            IsEnvironmentPanelVisible = true;
        }
        finally
        {
            _suppressEnvironmentAutoSave = previousSuppressEnvironmentAutoSave;
        }
    }

    [RelayCommand]
    private void NewEnvironment()
    {
        var previousSuppressEnvironmentAutoSave = _suppressEnvironmentAutoSave;
        _suppressEnvironmentAutoSave = true;
        try
        {
            ActiveEnvironment = null;
            NewEnvironmentName = string.Empty;
            ActiveEnvironmentVariables.Clear();
            IsEnvironmentPanelVisible = true;
            _logger.Information("Creating new environment");
        }
        finally
        {
            _suppressEnvironmentAutoSave = previousSuppressEnvironmentAutoSave;
        }
    }

    [RelayCommand]
    private async Task ExportEnvironmentsAsync()
    {
        var storageProvider = _getStorageProvider();
        if (storageProvider is null)
        {
            return;
        }

        var json = JsonSerializer.Serialize(new EnvironmentExportPayload(
            "arbor.httpclient.environments",
            1,
            Environments.Select(environment => new ExportEnvironment(
                    environment.Name,
                    environment.Variables
                        .Select(variable => new ExportEnvironmentVariable(variable.Name, variable.Value, variable.IsEnabled))
                        .ToList()))
                .ToList()),
            new JsonSerializerOptions { WriteIndented = true });

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Environments",
            SuggestedFileName = $"arbor-environments-{DateTime.UtcNow:yyyyMMddHHmmss}.json",
            FileTypeChoices =
            [
                new FilePickerFileType("JSON")
                {
                    Patterns = ["*.json"]
                }
            ]
        });

        if (file is null)
        {
            return;
        }

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(json);
        await writer.FlushAsync();
        _logger.Information("Exported environments to {Path}", file.Path.LocalPath);
    }

    private async Task SaveEnvironmentCoreAsync(bool closeEnvironmentPanel, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(NewEnvironmentName) || _isSavingEnvironment)
        {
            return;
        }

        _isSavingEnvironment = true;
        var previousSuppressEnvironmentAutoSave = _suppressEnvironmentAutoSave;
        _suppressEnvironmentAutoSave = true;
        try
        {
            var variables = ActiveEnvironmentVariables
                .Where(variable => !string.IsNullOrWhiteSpace(variable.Name))
                .Select(variable => new EnvironmentVariable(variable.Name, variable.Value, variable.IsEnabled))
                .ToList();

            int? newEnvironmentId = null;
            if (ActiveEnvironment is { } activeEnv)
            {
                await _environmentRepository.UpdateAsync(activeEnv.Id, NewEnvironmentName, variables, cancellationToken);
                _logger.Information("Updated environment {EnvironmentName}", NewEnvironmentName);
            }
            else
            {
                newEnvironmentId = await _environmentRepository.SaveAsync(NewEnvironmentName, variables, cancellationToken);
                _logger.Information("Created environment {EnvironmentName}", NewEnvironmentName);
            }

            if (!closeEnvironmentPanel)
            {
                _preserveFormStateOnSave = true;
            }
            try
            {
                await LoadEnvironmentsAsync(cancellationToken);

                if (newEnvironmentId.HasValue)
                {
                    ActiveEnvironment = Environments.FirstOrDefault(environment => environment.Id == newEnvironmentId.Value);
                }
            }
            finally
            {
                _preserveFormStateOnSave = false;
            }

            if (closeEnvironmentPanel)
            {
                IsEnvironmentPanelVisible = false;
            }
        }
        finally
        {
            _suppressEnvironmentAutoSave = previousSuppressEnvironmentAutoSave;
            _isSavingEnvironment = false;
        }
    }

    private void QueueEnvironmentAutoSave()
    {
        if (_suppressEnvironmentAutoSave ||
            _isSavingEnvironment ||
            !IsEnvironmentPanelVisible ||
            string.IsNullOrWhiteSpace(NewEnvironmentName) ||
            ActiveEnvironmentVariables.Any(variable => string.IsNullOrWhiteSpace(variable.Name)))
        {
            return;
        }

        _environmentAutoSaveCts?.Cancel();
        _environmentAutoSaveCts?.Dispose();
        _environmentAutoSaveCts = new CancellationTokenSource();
        _ = TriggerEnvironmentAutoSaveAsync(_environmentAutoSaveCts.Token);
    }

    private async Task TriggerEnvironmentAutoSaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1000), cancellationToken).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(async () => await SaveEnvironmentCoreAsync(closeEnvironmentPanel: false, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // Debounced auto-save was superseded by a newer edit.
        }
    }

    private void OnActiveEnvironmentVariablesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is { } newItems)
        {
            foreach (EnvironmentVariableViewModel variable in newItems)
            {
                variable.PropertyChanged += OnActiveEnvironmentVariablePropertyChanged;
            }
        }

        if (e.OldItems is { } oldItems)
        {
            foreach (EnvironmentVariableViewModel variable in oldItems)
            {
                variable.PropertyChanged -= OnActiveEnvironmentVariablePropertyChanged;
            }
        }

        _requestEditor.RefreshRequestPreview();
        QueueEnvironmentAutoSave();
    }

    private void OnActiveEnvironmentVariablePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _requestEditor.RefreshRequestPreview();
        QueueEnvironmentAutoSave();
    }

    public void Dispose()
    {
        _environmentAutoSaveCts?.Cancel();
        _environmentAutoSaveCts?.Dispose();
        _environmentAutoSaveCts = null;
    }
}
