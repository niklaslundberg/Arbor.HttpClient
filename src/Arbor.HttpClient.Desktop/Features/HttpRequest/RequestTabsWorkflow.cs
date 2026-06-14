using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;

namespace Arbor.HttpClient.Desktop.Features.HttpRequest;

/// <summary>
/// Owns the open request-tab list lifecycle: adding new tabs and closing tabs
/// while always keeping at least one tab open.
/// </summary>
public sealed class RequestTabsWorkflow
{
    /// <summary>The open request tabs. There is always at least one tab.</summary>
    public ObservableCollection<RequestTabViewModel> Tabs { get; } = [];

    /// <summary>Wraps <paramref name="requestEditor"/> in a new tab, adds it to <see cref="Tabs"/>, and returns it.</summary>
    public RequestTabViewModel AddTab(RequestEditorViewModel requestEditor)
    {
        var tab = new RequestTabViewModel(requestEditor);
        Tabs.Add(tab);
        return tab;
    }

    /// <summary>
    /// Closes <paramref name="tab"/> and disposes it, unless it is the only open tab
    /// (in which case nothing happens). Returns the tab that should become active when
    /// <paramref name="activeTab"/> was the closed tab, or <see langword="null"/> when
    /// the active tab does not need to change.
    /// </summary>
    public RequestTabViewModel? CloseTab(RequestTabViewModel? tab, RequestTabViewModel? activeTab)
    {
        if (tab is null || Tabs.Count <= 1)
        {
            return null;
        }

        RequestTabViewModel? nextActive = null;
        if (ReferenceEquals(activeTab, tab))
        {
            var index = Tabs.IndexOf(tab);
            Tabs.Remove(tab);
            var newIndex = Math.Max(0, Math.Min(index, Tabs.Count - 1));
            nextActive = Tabs[newIndex];
        }
        else
        {
            Tabs.Remove(tab);
        }

        tab.Dispose();
        return nextActive;
    }

    /// <summary>
    /// Finds the open tab whose collection-request source matches the given identity
    /// (<see cref="RequestTabViewModel.MatchesCollectionRequest"/>), or <see langword="null"/>
    /// when no tab matches.
    /// </summary>
    public static RequestTabViewModel? FindMatchingTab(
        IEnumerable<RequestTabViewModel> tabs,
        int collectionId,
        string method,
        string path,
        string name) =>
        tabs.FirstOrDefault(tab => tab.MatchesCollectionRequest(collectionId, method, path, name));

    /// <summary>
    /// Returns the bytes of <paramref name="responseBodyBytes"/> as a <see cref="byte"/> array,
    /// reusing the backing array without copying when <paramref name="responseBodyBytes"/>
    /// already wraps a whole array (the common case for a freshly received response).
    /// </summary>
    public static byte[] GetResponseStateBytes(ReadOnlyMemory<byte> responseBodyBytes)
    {
        if (responseBodyBytes.IsEmpty)
        {
            return [];
        }

        if (MemoryMarshal.TryGetArray(responseBodyBytes, out ArraySegment<byte> segment)
            && segment.Array is { } array)
        {
            if (segment.Offset == 0 && segment.Count == array.Length)
            {
                return array;
            }

            return responseBodyBytes.ToArray();
        }

        return responseBodyBytes.ToArray();
    }
}
