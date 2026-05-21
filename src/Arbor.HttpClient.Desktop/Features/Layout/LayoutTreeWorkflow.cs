using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;

namespace Arbor.HttpClient.Desktop.Features.Layout;

/// <summary>
/// Owns dock-tree snapshot capture and restore orchestration for MainWindow layout persistence.
/// </summary>
public sealed class LayoutTreeWorkflow
{
    [SuppressMessage("Major Code Smell", "S3776", Justification = "Captures snapshot, floating window filtering, and geometry in one cohesive traversal.")]
    public DockLayoutSnapshot? CaptureLayoutSnapshot(
        IRootDock? root,
        DockTreeNode? cachedDockTree,
        double windowWidthAtClose,
        double windowHeightAtClose,
        int windowXAtClose,
        int windowYAtClose,
        bool windowPositionCaptured)
    {
        if (root is null)
        {
            return null;
        }

        var leftToolDock = FindDockById<ToolDock>(root, "left-tool-dock");
        var documentLayout = FindDockById<ProportionalDock>(root, "document-layout");
        var requestDock = FindDockById<DocumentDock>(root, "request-dock");
        if (leftToolDock is null || documentLayout is null || requestDock is null)
        {
            return null;
        }

        var floatingWindows = new List<FloatingWindowSnapshot>();
        if (root.Windows is { } windows)
        {
            foreach (var window in windows)
            {
                var floatRoot = window.Layout;
                if (floatRoot is null)
                {
                    continue;
                }

                var ids = new List<string>();
                CollectDockableIds(floatRoot, ids);

                if (ContainsPinnedRequestResponseDockable(ids))
                {
                    continue;
                }

                floatingWindows.Add(new FloatingWindowSnapshot
                {
                    X = window.X,
                    Y = window.Y,
                    Width = window.Width > 0 ? window.Width : 300,
                    Height = window.Height > 0 ? window.Height : 400,
                    DockableIds = ids,
                    ActiveDockableId = floatRoot.ActiveDockable?.Id
                });
            }
        }

        return new DockLayoutSnapshot
        {
            LeftToolProportion = SanitizeProportion(leftToolDock.Proportion, 0.25),
            DocumentProportion = SanitizeProportion(documentLayout.Proportion, 0.75),
            RequestDockProportion = SanitizeOptionalProportion(requestDock.Proportion),
            ResponseDockProportion = 0,
            ActiveToolDockableId = leftToolDock.ActiveDockable?.Id,
            LeftToolDockableOrder = GetDockableOrder(leftToolDock.VisibleDockables),
            FloatingWindows = floatingWindows,
            DockTree = cachedDockTree,
            WindowWidth = windowWidthAtClose > 0 ? windowWidthAtClose : 0,
            WindowHeight = windowHeightAtClose > 0 ? windowHeightAtClose : 0,
            WindowX = windowXAtClose,
            WindowY = windowYAtClose,
            HasWindowPosition = windowPositionCaptured
        };
    }

    public DockTreeNode? CaptureDockTree(IRootDock? root) => root is { } layoutRoot ? CaptureDockNode(layoutRoot) : null;

    [SuppressMessage("Major Code Smell", "S3776", Justification = "Applies tree rebuild, fallback proportion restore, and floating window restore in one orchestration entry point.")]
    public LayoutApplyResult ApplyLayoutSnapshot(
        DockLayoutSnapshot? snapshot,
        IRootDock? layout,
        DockFactory? dockFactory)
    {
        if (snapshot is null || layout is null || dockFactory is null)
        {
            return new LayoutApplyResult(layout, false);
        }

        var effectiveLayout = layout;

        if (snapshot.DockTree is { } treeNode)
        {
            if (effectiveLayout.Windows is { Count: > 0 } || DockTreeRequiresRebuild(treeNode, effectiveLayout))
            {
                var defaultLayout = dockFactory.CreateLayout();
                dockFactory.InitLayout(defaultLayout);

                var leafDockables = new Dictionary<string, IDockable>(StringComparer.OrdinalIgnoreCase);
                CollectLeafDockables(defaultLayout, leafDockables);

                var newRoot = BuildDockNode(treeNode, leafDockables, dockFactory) as RootDock;
                if (newRoot is { })
                {
                    dockFactory.InitLayout(newRoot);
                    effectiveLayout = newRoot;
                    dockFactory.UpdateLeftToolDock();
                }
            }
            else
            {
                ApplyDockTreeInPlace(effectiveLayout, treeNode);
            }
        }
        else
        {
            if (effectiveLayout.Windows is { Count: > 0 })
            {
                effectiveLayout = dockFactory.CreateLayout();
                dockFactory.InitLayout(effectiveLayout);
            }

            var leftToolDock = FindDockById<ToolDock>(effectiveLayout, "left-tool-dock");
            var documentLayout = FindDockById<ProportionalDock>(effectiveLayout, "document-layout");
            var requestDock = FindDockById<DocumentDock>(effectiveLayout, "request-dock");
            if (leftToolDock is null || documentLayout is null || requestDock is null)
            {
                return new LayoutApplyResult(effectiveLayout, true);
            }

            if (snapshot.LeftToolProportion > 0)
            {
                leftToolDock.Proportion = snapshot.LeftToolProportion;
            }

            if (snapshot.DocumentProportion > 0)
            {
                documentLayout.Proportion = snapshot.DocumentProportion;
            }

            if (snapshot.RequestDockProportion > 0)
            {
                requestDock.Proportion = snapshot.RequestDockProportion;
            }


            ApplyDockOrder(leftToolDock, snapshot.LeftToolDockableOrder);
            SetActiveDockable(leftToolDock, snapshot.ActiveToolDockableId);
        }

        foreach (var floatingWindow in snapshot.FloatingWindows)
        {
            if (floatingWindow.DockableIds.Count == 0 || ContainsPinnedRequestResponseDockable(floatingWindow.DockableIds))
            {
                continue;
            }

            IDockable? primary = null;
            foreach (var id in floatingWindow.DockableIds)
            {
                primary = FindDockById<IDockable>(effectiveLayout, id);
                if (primary is { })
                {
                    break;
                }
            }

            if (primary is null)
            {
                continue;
            }

            var countBefore = effectiveLayout.Windows?.Count ?? 0;
            dockFactory.FloatDockable(primary);

            if (effectiveLayout.Windows is null || effectiveLayout.Windows.Count <= countBefore)
            {
                continue;
            }

            var floatingLayoutWindow = effectiveLayout.Windows[^1];
            floatingLayoutWindow.X = floatingWindow.X;
            floatingLayoutWindow.Y = floatingWindow.Y;
            floatingLayoutWindow.Width = floatingWindow.Width;
            floatingLayoutWindow.Height = floatingWindow.Height;

            if (floatingLayoutWindow.Layout is { } floatDock)
            {
                for (var i = 1; i < floatingWindow.DockableIds.Count; i++)
                {
                    var extra = FindDockById<IDockable>(effectiveLayout, floatingWindow.DockableIds[i]);
                    if (extra?.Owner is IDock sourceOwner)
                    {
                        dockFactory.MoveDockable(sourceOwner, floatDock, extra, null);
                    }
                }

                SetActiveDockable(floatDock, floatingWindow.ActiveDockableId);
            }
        }

        return new LayoutApplyResult(effectiveLayout, true);
    }

    private static double SanitizeProportion(double value, double fallback) =>
        double.IsFinite(value) && value > 0 ? value : fallback;

    private static double SanitizeOptionalProportion(double value) =>
        double.IsFinite(value) && value >= 0 ? value : 0;

    private static DockTreeNode? CaptureDockNode(IDockable dockable)
    {
        if (dockable is IRootDock root)
        {
            var children = (root.VisibleDockables ?? [])
                .Select(CaptureDockNode)
                .OfType<DockTreeNode>()
                .ToList();

            return new DockTreeNode
            {
                Type = "Root",
                Id = root.Id,
                Proportion = SanitizeOptionalProportion(root.Proportion),
                Children = children
            };
        }

        if (dockable is IProportionalDock proportional)
        {
            var children = (proportional.VisibleDockables ?? [])
                .Select(CaptureDockNode)
                .OfType<DockTreeNode>()
                .ToList();

            return new DockTreeNode
            {
                Type = "Proportional",
                Id = proportional.Id,
                Proportion = SanitizeOptionalProportion(proportional.Proportion),
                Orientation = proportional.Orientation.ToString(),
                Children = children
            };
        }

        if (dockable is IProportionalDockSplitter splitter)
        {
            return new DockTreeNode
            {
                Type = "Splitter",
                Id = splitter.Id,
                Proportion = SanitizeOptionalProportion(splitter.Proportion)
            };
        }

        if (dockable is IToolDock toolDock)
        {
            var contentIds = (toolDock.VisibleDockables ?? [])
                .Where(content => !string.IsNullOrWhiteSpace(content.Id))
                .Select(content => content.Id)
                .ToList();

            return new DockTreeNode
            {
                Type = "Tool",
                Id = toolDock.Id,
                Proportion = SanitizeOptionalProportion(toolDock.Proportion),
                Alignment = toolDock.Alignment.ToString(),
                GripMode = toolDock.GripMode.ToString(),
                IsCollapsable = toolDock.IsCollapsable,
                ContentIds = contentIds,
                ActiveContentId = toolDock.ActiveDockable?.Id
            };
        }

        if (dockable is IDocumentDock documentDock)
        {
            var contentIds = (documentDock.VisibleDockables ?? [])
                .Where(content => !string.IsNullOrWhiteSpace(content.Id))
                .Select(content => content.Id)
                .ToList();

            return new DockTreeNode
            {
                Type = "Document",
                Id = documentDock.Id,
                Proportion = SanitizeOptionalProportion(documentDock.Proportion),
                IsCollapsable = documentDock.IsCollapsable,
                ContentIds = contentIds,
                ActiveContentId = documentDock.ActiveDockable?.Id
            };
        }

        return null;
    }

    private static bool ContainsPinnedRequestResponseDockable(IEnumerable<string> dockableIds) =>
        dockableIds.Any(IsPinnedRequestResponseDockableId);

    private static bool IsPinnedRequestResponseDockableId(string dockableId) =>
        string.Equals(dockableId, "request", StringComparison.OrdinalIgnoreCase)
        || IsLegacyResponseDockableId(dockableId);

    private static bool IsLegacyResponseDockableId(string dockableId) =>
        string.Equals(dockableId, "response", StringComparison.OrdinalIgnoreCase)
        || string.Equals(dockableId, "response-dock", StringComparison.OrdinalIgnoreCase);

    private static void CollectLeafDockables(IDockable dockable, Dictionary<string, IDockable> result)
    {
        if (dockable is IProportionalDockSplitter)
        {
            return;
        }

        if (dockable is IDock dock && dock.VisibleDockables is { } dockables)
        {
            foreach (var child in dockables)
            {
                CollectLeafDockables(child, result);
            }
        }
        else if (!string.IsNullOrWhiteSpace(dockable.Id))
        {
            result[dockable.Id] = dockable;
        }
    }

    [SuppressMessage("Major Code Smell", "S3776", Justification = "Type-driven dock node factory maps serialized node variants to Dock model instances.")]
    private static IDockable? BuildDockNode(
        DockTreeNode node,
        IReadOnlyDictionary<string, IDockable> contentDockables,
        DockFactory dockFactory)
    {
        switch (node.Type)
        {
            case "Root":
            {
                var children = dockFactory.CreateList<IDockable>();
                foreach (var child in node.Children
                    .Select(childNode => BuildDockNode(childNode, contentDockables, dockFactory))
                    .Where(child => child is not null))
                {
                    children.Add(child!);
                }

                var rootDock = new RootDock
                {
                    Id = node.Id ?? "root",
                    IsCollapsable = false,
                    VisibleDockables = children,
                    Windows = dockFactory.CreateList<IDockWindow>()
                };
                rootDock.ActiveDockable = rootDock.DefaultDockable = children.FirstOrDefault();
                return rootDock;
            }

            case "Proportional":
            {
                var children = dockFactory.CreateList<IDockable>();
                foreach (var child in node.Children
                    .Select(childNode => BuildDockNode(childNode, contentDockables, dockFactory))
                    .Where(child => child is not null))
                {
                    children.Add(child!);
                }

                var proportionalDock = new ProportionalDock
                {
                    Id = node.Id ?? Guid.NewGuid().ToString("D"),
                    Proportion = node.Proportion,
                    Orientation = string.Equals(node.Orientation, "Vertical", StringComparison.OrdinalIgnoreCase)
                        ? Orientation.Vertical
                        : Orientation.Horizontal,
                    VisibleDockables = children
                };
                proportionalDock.ActiveDockable = children.FirstOrDefault(dockable => dockable is not IProportionalDockSplitter);
                return proportionalDock;
            }

            case "Splitter":
                return new ProportionalDockSplitter
                {
                    Id = node.Id ?? Guid.NewGuid().ToString("D"),
                    Proportion = node.Proportion
                };

            case "Tool":
            {
                var contents = dockFactory.CreateList<IDockable>();
                foreach (var content in node.ContentIds
                    .Where(contentDockables.ContainsKey)
                    .Select(id => contentDockables[id]))
                {
                    contents.Add(content);
                }

                if (contents.Count == 0)
                {
                    return null;
                }

                IDockable? active = null;
                if (node.ActiveContentId is { } activeId)
                {
                    contentDockables.TryGetValue(activeId, out active);
                }

                active ??= contents.FirstOrDefault();

                return new ToolDock
                {
                    Id = node.Id ?? Guid.NewGuid().ToString("D"),
                    Proportion = node.Proportion,
                    Alignment = ParseAlignment(node.Alignment),
                    GripMode = ParseGripMode(node.GripMode),
                    IsCollapsable = node.IsCollapsable,
                    VisibleDockables = contents,
                    ActiveDockable = active
                };
            }

            case "Document":
            {
                var contents = dockFactory.CreateList<IDockable>();
                foreach (var content in node.ContentIds
                    .Where(contentDockables.ContainsKey)
                    .Select(id => contentDockables[id])
                    .Where(content => !IsLegacyResponseDockableId(content.Id ?? string.Empty)))
                {
                    contents.Add(content);
                }

                if (contents.Count == 0)
                {
                    return null;
                }

                IDockable? active = null;
                if (node.ActiveContentId is { } activeId && !IsLegacyResponseDockableId(activeId))
                {
                    contentDockables.TryGetValue(activeId, out active);
                }

                active ??= contents.FirstOrDefault();

                return new DocumentDock
                {
                    Id = node.Id ?? Guid.NewGuid().ToString("D"),
                    Proportion = node.Proportion,
                    IsCollapsable = node.IsCollapsable,
                    VisibleDockables = contents,
                    ActiveDockable = active
                };
            }

            default:
                return null;
        }
    }

    private static Alignment ParseAlignment(string? value) =>
        value switch
        {
            "Left" => Alignment.Left,
            "Right" => Alignment.Right,
            "Top" => Alignment.Top,
            "Bottom" => Alignment.Bottom,
            _ => Alignment.Unset
        };

    private static GripMode ParseGripMode(string? value) =>
        value switch
        {
            "Visible" => GripMode.Visible,
            "AutoHide" => GripMode.AutoHide,
            "Hidden" => GripMode.Hidden,
            _ => GripMode.Visible
        };

    private static void CollectDockableIds(IDockable dockable, ICollection<string> ids)
    {
        if (!string.IsNullOrWhiteSpace(dockable.Id))
        {
            ids.Add(dockable.Id);
        }

        if (dockable is IDock dock && dock.VisibleDockables is { } dockables)
        {
            foreach (var child in dockables)
            {
                CollectDockableIds(child, ids);
            }
        }
    }

    private static bool DockTreeRequiresRebuild(DockTreeNode node, IDockable currentRoot) =>
        DockTreeNodeRequiresRebuild(node, currentRoot);

    private static bool DockTreeNodeRequiresRebuild(DockTreeNode node, IDockable currentRoot)
    {
        if (node.Type is "Document" && string.Equals(node.Id, "response-dock", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (node.Type is "Tool" or "Document")
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                return true;
            }

            if (FindDockById<IDock>(currentRoot, node.Id) is not { } existing)
            {
                return true;
            }

            var currentIds = GetDockableOrder(existing.VisibleDockables);
            if (!currentIds.SequenceEqual(node.ContentIds, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            if (node.Type is "Tool" && existing is ToolDock existingToolDock && existingToolDock.Alignment != ParseAlignment(node.Alignment))
            {
                return true;
            }

            return false;
        }

        return node.Children.Any(child => DockTreeNodeRequiresRebuild(child, currentRoot));
    }

    [SuppressMessage("Major Code Smell", "S3776", Justification = "Recursive in-place tree apply updates proportions, ordering, and active items.")]
    private static void ApplyDockTreeInPlace(IDockable currentRoot, DockTreeNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.Id) && node.Proportion > 0)
        {
            var dockable = FindDockById<IDockable>(currentRoot, node.Id);
            if (dockable is { })
            {
                dockable.Proportion = node.Proportion;
            }
        }

        if (node.Type is "Tool" or "Document" && !string.IsNullOrWhiteSpace(node.Id))
        {
            var dock = FindDockById<IDock>(currentRoot, node.Id);
            if (dock is { })
            {
                if (node.ContentIds.Count > 0)
                {
                    ApplyDockOrder(dock, node.ContentIds);
                }

                if (!string.IsNullOrWhiteSpace(node.ActiveContentId))
                {
                    SetActiveDockable(dock, node.ActiveContentId);
                }

                if (node.Type is "Tool" && dock is ToolDock toolDock)
                {
                    toolDock.GripMode = ParseGripMode(node.GripMode);
                }
            }
        }

        foreach (var child in node.Children)
        {
            ApplyDockTreeInPlace(currentRoot, child);
        }
    }

    private static void ApplyDockOrder(IDock dock, IReadOnlyList<string> orderedDockableIds)
    {
        var visibleDockables = dock.VisibleDockables;
        if (visibleDockables is null || orderedDockableIds.Count == 0)
        {
            return;
        }

        var byId = visibleDockables
            .Where(dockable => !string.IsNullOrWhiteSpace(dockable.Id))
            .ToDictionary(dockable => dockable.Id, StringComparer.OrdinalIgnoreCase);

        var reordered = new List<IDockable>(visibleDockables.Count);
        reordered.AddRange(orderedDockableIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .Where(dockable => !reordered.Contains(dockable)));

        reordered.AddRange(visibleDockables.Where(dockable => !reordered.Contains(dockable)));

        visibleDockables.Clear();
        foreach (var dockable in reordered)
        {
            visibleDockables.Add(dockable);
        }
    }

    private static void SetActiveDockable(IDock dock, string? dockableId)
    {
        if (string.IsNullOrWhiteSpace(dockableId) || dock.VisibleDockables is null)
        {
            return;
        }

        var activeDockable = dock.VisibleDockables.FirstOrDefault(item => string.Equals(item.Id, dockableId, StringComparison.OrdinalIgnoreCase));
        if (activeDockable is { } active)
        {
            dock.ActiveDockable = active;
        }
    }

    private static T? FindDockById<T>(IDockable dockable, string id) where T : class, IDockable
    {
        if (dockable is T foundDockable && string.Equals(foundDockable.Id, id, StringComparison.OrdinalIgnoreCase))
        {
            return foundDockable;
        }

        if (dockable is IDock dock && dock.VisibleDockables is { } childDockables)
        {
            return childDockables
                .Select(child => FindDockById<T>(child, id))
                .FirstOrDefault(childDock => childDock is not null);
        }

        return null;
    }

    private static List<string> GetDockableOrder(IList<IDockable>? dockables) =>
        dockables?
            .Select(dockable => dockable.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToList() ?? [];
}

public sealed record LayoutApplyResult(IRootDock? Layout, bool Applied);
