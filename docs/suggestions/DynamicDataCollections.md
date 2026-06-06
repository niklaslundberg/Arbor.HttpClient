# Task: Introduce DynamicData for Collections panel

**Description**
- Replace the current `ObservableCollection<CollectionGroupViewModel>` in the Collections feature with a DynamicData `SourceCache`/`ObservableChangeSet`.
- This will provide built‑in filtering, sorting, and change‑set notifications.

**Acceptance Criteria**
1. `src/Arbor.HttpClient.Desktop/Features/Collections/CollectionsWorkflowViewModel.cs` creates a `SourceCache<CollectionGroupViewModel, string>` keyed by the group ID.
2. Exposes a read‑only `ReadOnlyObservableCollection<CollectionGroupViewModel>` (`FilteredGroups`).
3. Implements filtering based on a searchable text property using `.Filter(this.WhenAnyValue(x => x.FilterText).Select(BuildFilter))`.
4. Sorting is applied with `SortExpressionComparer<CollectionGroupViewModel>.Ascending(g => g.Name)`.
5. UI bindings are unchanged – they bind to `FilteredGroups`.
6. All existing collection‑related UI tests continue to pass.

**Tests to Create**
- Unit test using `TestScheduler` to verify that updating `FilterText` reshapes `FilteredGroups` deterministically.
- Test that adding a new `CollectionGroupViewModel` to the source updates the bound collection.
- Test that sorting respects the defined comparator.
- Ensure that disposing the view model disposes the DynamicData subscription.