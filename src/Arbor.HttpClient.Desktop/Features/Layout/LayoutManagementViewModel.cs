using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Arbor.HttpClient.Desktop.Features.Layout;

/// <summary>
/// Owns the named-layout collection (<see cref="SavedLayoutNames"/>/<see cref="SelectedLayoutName"/>)
/// and the four layout commands (save as new/save to existing/restore default/remove). The
/// dock-tree/window-geometry pipeline (capture/apply/persist) stays owned by
/// <c>MainWindowViewModel</c> and is supplied here via constructor-injected delegates, matching
/// the "Apply* UI projections stay in Main" precedent.
/// </summary>
public sealed partial class LayoutManagementViewModel : Tool, IDisposable
{
    private readonly LayoutWorkflow _layoutWorkflow = new();
    private readonly Action _refreshDockTreeCache;
    private readonly Func<DockLayoutSnapshot?> _captureLayoutSnapshot;
    private readonly Action<DockLayoutSnapshot?> _applyLayoutSnapshot;
    private readonly Action _persistLayoutOptions;
    private readonly Func<DockLayoutSnapshot?> _getDefaultLayout;
    private readonly CompositeDisposable _disposables = new();
    private bool _suppressLayoutRestore;

    [Reactive]
    private string? _selectedLayoutName;

    public LayoutManagementViewModel(
        Action refreshDockTreeCache,
        Func<DockLayoutSnapshot?> captureLayoutSnapshot,
        Action<DockLayoutSnapshot?> applyLayoutSnapshot,
        Action persistLayoutOptions,
        Func<DockLayoutSnapshot?> getDefaultLayout)
    {
        _refreshDockTreeCache = refreshDockTreeCache;
        _captureLayoutSnapshot = captureLayoutSnapshot;
        _applyLayoutSnapshot = applyLayoutSnapshot;
        _persistLayoutOptions = persistLayoutOptions;
        _getDefaultLayout = getDefaultLayout;

        Id = "layout-management";
        Title = "Layout";

        _disposables.Add(this
            .WhenAnyValue(viewModel => viewModel.SelectedLayoutName)
            .Skip(1)
            .Subscribe(_ => ApplySelectedLayoutName()));
    }

    /// <summary>Names of the saved layouts, ordered alphabetically. Bound to the layout-selection combo box.</summary>
    public ObservableCollection<string> SavedLayoutNames => _layoutWorkflow.SavedLayoutNames;

    /// <summary>
    /// Replaces the saved layouts from <paramref name="layouts"/> and selects the first one
    /// (alphabetically), without triggering <see cref="ApplySelectedLayoutName"/>'s restore side effect.
    /// </summary>
    public void LoadFromOptions(LayoutOptions? layouts)
    {
        _suppressLayoutRestore = true;
        SelectedLayoutName = _layoutWorkflow.LoadFromOptions(layouts);
        _suppressLayoutRestore = false;
    }

    /// <summary>Builds the persisted list of named layouts, ordered alphabetically by name.</summary>
    public IReadOnlyList<NamedDockLayout> BuildNamedLayouts() => _layoutWorkflow.BuildNamedLayouts();

    private void ApplySelectedLayoutName()
    {
        if (_suppressLayoutRestore || string.IsNullOrWhiteSpace(SelectedLayoutName))
        {
            return;
        }

        if (_layoutWorkflow.TryGetLayout(SelectedLayoutName, out var snapshot))
        {
            _applyLayoutSnapshot(snapshot);
            _persistLayoutOptions();
        }
    }

    [ReactiveCommand]
    private void SaveLayoutAsNew()
    {
        _refreshDockTreeCache();
        var savedLayoutName = _layoutWorkflow.SaveLayoutAsNew(_captureLayoutSnapshot);
        if (string.IsNullOrWhiteSpace(savedLayoutName))
        {
            return;
        }

        SetSelectedLayoutNameQuietly(savedLayoutName);
        _persistLayoutOptions();
    }

    [ReactiveCommand]
    private void SaveLayoutToExisting(string? layoutName)
    {
        _refreshDockTreeCache();
        if (!_layoutWorkflow.SaveLayoutToExisting(layoutName, _captureLayoutSnapshot))
        {
            return;
        }

        SetSelectedLayoutNameQuietly(layoutName);
        _persistLayoutOptions();
    }

    [ReactiveCommand]
    private void RestoreDefaultLayout()
    {
        if (!_layoutWorkflow.RestoreDefaultLayout(_getDefaultLayout(), _applyLayoutSnapshot))
        {
            return;
        }

        SetSelectedLayoutNameQuietly(null);
        _persistLayoutOptions();
    }

    [ReactiveCommand]
    private void RemoveLayout(string? layoutName)
    {
        var removeLayoutResult = _layoutWorkflow.RemoveLayout(layoutName, SelectedLayoutName);
        if (!removeLayoutResult.Removed)
        {
            return;
        }

        SetSelectedLayoutNameQuietly(removeLayoutResult.SelectedLayoutName);
        _persistLayoutOptions();
    }

    private void SetSelectedLayoutNameQuietly(string? layoutName)
    {
        _suppressLayoutRestore = true;
        SelectedLayoutName = layoutName;
        _suppressLayoutRestore = false;
    }

    public void Dispose() => _disposables.Dispose();
}
