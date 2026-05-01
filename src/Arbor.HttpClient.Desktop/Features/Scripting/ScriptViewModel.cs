using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Arbor.HttpClient.Desktop.Shared;

namespace Arbor.HttpClient.Desktop.Features.Scripting;

/// <summary>
/// View model for the Script editor tab in the request panel.
/// Stores the pre-request and post-response C# script texts, compilation/runtime
/// errors, and the script log output. Intentionally has no Avalonia dependency so
/// it can be tested without a headless session.
/// </summary>
public sealed partial class ScriptViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _preRequestScript = string.Empty;

    [ObservableProperty]
    private string _postResponseScript = string.Empty;

    [ObservableProperty]
    private bool _hasErrors;

    [ObservableProperty]
    private bool _hasLog;

    public ObservableCollection<string> Errors { get; } = [];
    public ObservableCollection<string> Log { get; } = [];

    [RelayCommand]
    private void ClearLog()
    {
        Log.Clear();
        HasLog = false;
    }

    [RelayCommand]
    private void ClearErrors()
    {
        Errors.Clear();
        HasErrors = false;
    }

    internal void SetResult(Core.Scripting.ScriptResult result)
    {
        Errors.Clear();
        foreach (var error in result.Errors)
        {
            Errors.Add(error);
        }

        foreach (var entry in result.Log)
        {
            Log.Add(entry);
        }

        HasErrors = Errors.Count > 0;
        HasLog = Log.Count > 0;
    }

    internal void ClearPreviousRun()
    {
        Errors.Clear();
        HasErrors = false;
    }
}
