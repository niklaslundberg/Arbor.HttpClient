# MainWindowViewModel Split Plan

## Context

`MainWindowViewModel` contains multiple feature areas and coordination responsibilities in one class (about 4,000 lines when this plan was written; about 2,985 lines as of 2026-06-12 after the named-layouts slice; about 2,920 lines as of 2026-06-13 after the response-actions Phase 3 cleanup; about 2,782 lines as of 2026-06-13 after extracting the collection-request editor projection; about 2,710 lines as of 2026-06-14 after extracting the request-body external-edit pipeline; about 2,666 lines as of 2026-06-14 (later) after moving the `ReapplyStartupLayout` dock-tree proportion-reapply helpers into `LayoutTreeWorkflow`; about 2,658 lines as of 2026-06-14 (later 2) after moving the `IsInheritedHeader`/`HasManualHeaderOverride` helpers into `CollectionInheritedHeadersWorkflow`; about 2,603 lines as of 2026-06-14 (later 3) after removing the dead `FormatElapsedMilliseconds`/`FormatByteSize` statics, moving `GetResponseStateBytes` into `RequestTabsWorkflow`, extracting the temp-file cleanup loop into `ResponseActionsViewModel.DeleteTempFiles`, deduplicating the `SendHttpRequestAsync`/`SendGraphQlRequestAsync` outcome handling, and moving the `SyncDefaultContentTypeSelection` decision logic into `ApplicationOptionsWorkflow.ResolveDefaultContentTypeSelection`; about 2,585 lines as of 2026-06-14 (later 4) after moving the collection-request lookup into `CollectionInheritedHeadersWorkflow.FindMatchingRequest` and the request-URL/theme/font-family/font-size decision helpers into `ApplicationOptionsWorkflow`; about 2,567 lines as of 2026-06-14 (later 5) after moving the existing-tab lookup into `RequestTabsWorkflow.FindMatchingTab`, the inherited-header property-change merge into `CollectionInheritedHeadersWorkflow.ObservePropertyChanges`, and the `ApplySelectedCollection` collection-item/inherited-header projection loops into `CollectionFilterWorkflow.BuildCollectionItems`/`CollectionInheritedHeadersWorkflow.BuildHeaderViewModels`). This slows down feature work, increases regression risk, and makes focused testing harder.

This document is a persisted implementation plan for splitting feature logic from `MainWindowViewModel` while keeping behavior stable.

> **Status update (2026-06-11):** Several slices have been extracted since this plan was written, and the solution has since completed a full ReactiveUI/Rx.NET migration (see [`reactiveui-migration-progress.md`](reactiveui-migration-progress.md)), which changes the communication-pattern recommendation below. Current status per slice is tracked in [Extraction status](#extraction-status-2026-06-11) and the forward plan in [Remaining slices — ordered plan](#remaining-slices--ordered-plan-2026-06-11).

## Established extraction convention (Workflow / Coordinator)

The extractions completed so far converged on a two-layer convention. Follow it for the remaining slices:

- **`*Workflow`** — a plain `sealed` class with constructor-injected dependencies that owns one feature pipeline (e.g. `HttpRequestWorkflow`, `CollectionsWorkflow`, `LayoutTreeWorkflow`). It holds no UI state and returns immutable result records (e.g. `HttpRequestExecutionResult`) instead of mutating the caller.
- **`*Coordinator`** — a `sealed` class that orchestrates a workflow plus its side effects (persistence, reload callbacks, logging) and returns an outcome record (e.g. `ManualHttpRequestCoordinator` → `ManualHttpRequestOutcome`). Exception handling and cancellation semantics live here.
- **`MainWindowViewModel`** — keeps the `[Reactive]` UI state and `[ReactiveCommand]` entry points, calls the coordinator, and projects the outcome record onto UI properties (e.g. via `HttpResponseProjectionWorkflow`).

Both layers are headless-testable without the Avalonia runtime, which is the main reason for the split.

## Goals

- Keep `MainWindowViewModel` small and focused on composition/orchestration.
- Move feature-specific logic to feature-specific ViewModels/services.
- Improve test isolation with smaller, specialized test classes.
- Make new features additive (new folder/class) instead of requiring broad edits.
- Preserve current UX and persistence behavior during migration.

## Non-goals

- No broad rewrite in one PR.
- No behavior changes unrelated to feature separation.
- No forced DI container adoption unless composition friction remains after slice extraction.

## Current responsibility clusters to extract

Start with these clusters currently owned in `MainWindowViewModel`:

1. **Request execution flow** (HTTP/GraphQL/WebSocket/SSE dispatch and response handling)
2. **Collections workflows** (create/rename/delete/import/add current request)
3. **Scheduled job workflows** (create/start/stop/remove and persistence hooks)
4. **Options import/export and autosave triggers**
5. **Draft persistence and restore loop**
6. **Demo server start/stop/seed lifecycle**
7. **Response export/copy/open-in-editor actions**

## Target structure

## `Arbor.HttpClient.Desktop/Features/Main`

- `MainWindowViewModel` (composition only)
- `IMainWindowFeatureCoordinator` (optional minimal abstraction for orchestration)

## `Arbor.HttpClient.Desktop/Features/*`

- Feature-scoped orchestrators where logic currently lives in Main:
  - `HttpRequestWorkflowViewModel` (or coordinator)
  - `CollectionsWorkflowViewModel`
  - `ScheduledJobsWorkflowViewModel`
  - `ApplicationOptionsWorkflowViewModel`
  - `DraftWorkflowViewModel`
  - `DemoServerWorkflowViewModel`
  - `ResponseActionsViewModel`

Each extracted unit should own:

- Its own state + commands
- Its own dependencies (constructor-injected)
- Its own focused tests

`MainWindowViewModel` should only:

- Construct/receive feature VMs
- Route top-level commands to feature VMs
- Keep cross-feature composition glue minimal and explicit

## Phased execution plan

### Phase 1 — Define boundaries and contracts ✅ Implemented

> Implemented in this PR — `src/Arbor.HttpClient.Desktop/Features/HttpRequest/IResponseActionsContext.cs`

- Introduce focused interfaces for each extraction candidate where needed.
- Keep existing behavior paths unchanged.
- Add adapter/proxy members in `MainWindowViewModel` to avoid XAML churn in the same PR.

**Exit criteria:** boundaries compile, no UX changes, no Phase 1-specific test changes required. ✅

### Phase 2 — Extract one vertical slice at a time (repeated small PRs)

Recommended extraction order (lowest coupling first):

1. ✅ Response actions (copy/save/open external) — see below
2. ✅ Demo server lifecycle — `DemoDataWorkflow` + `DemoServerLifecycleCoordinator` (`Features/Demo/`)
3. ✅ Collections workflows — `CollectionsWorkflow` + `CollectionsManagementCoordinator` (create/rename/add/import/delete), `CollectionInheritedHeadersWorkflow` (debounced autosave + persistence), and `CollectionFilterWorkflow` (filter/sort/group) in `Features/Collections/`; Main keeps form-visibility state and editor live-preview projection
4. ✅ Scheduled jobs workflows — `ScheduledJobsWorkflow` (`Features/ScheduledJobs/`) owns the job-list lifecycle (add/remove/load incl. auto-start on launch); Main keeps the `[ReactiveCommand]` entry points and left-panel tab state
5. ✅ Request execution orchestration — `HttpRequestWorkflow` + `ManualHttpRequestCoordinator` + `HttpResponseProjectionWorkflow` (`Features/HttpRequest/`), `GraphQlRequestWorkflow` + `ManualGraphQlRequestCoordinator` (`Features/GraphQl/`), `StreamingConnectionWorkflow` (`Features/Streaming/`)
6. Draft/options autosave orchestration — options `Apply*`/save/import/export and the autosave subjects are still in `MainWindowViewModel`; layout has partial extraction via `LayoutWorkflow` + `LayoutTreeWorkflow` (`Features/Layout/`)

<a id="extraction-status-2026-06-11"></a>
#### Extraction status (2026-06-11)

| Slice | Extracted classes | Status |
|---|---|---|
| Response actions | `ResponseActionsViewModel` | ✅ Done |
| Request execution (HTTP) | `HttpRequestWorkflow`, `ManualHttpRequestCoordinator`, `HttpRequestExecutionResult`, `ManualHttpRequestOutcome` | ✅ Done |
| Response projection | `HttpResponseProjectionWorkflow` | ✅ Done |
| GraphQL execution | `GraphQlRequestWorkflow`, `ManualGraphQlRequestCoordinator` | ✅ Done |
| WebSocket/SSE connection | `StreamingConnectionWorkflow` | ✅ Done |
| Demo server lifecycle | `DemoDataWorkflow`, `DemoServerLifecycleCoordinator` | ✅ Done |
| Collections persistence | `CollectionsWorkflow`, `CollectionsManagementCoordinator` | ✅ Done — import/delete moved into the coordinator (2026-06-12) |
| Collections filter/sort/group | `CollectionFilterWorkflow` | ✅ Done (2026-06-12) |
| Collections inherited-headers auto-save | `CollectionInheritedHeadersWorkflow` | ✅ Done (2026-06-12) — debounce, suppression scopes, pending/flush, persistence; Main keeps dispatcher marshalling and the live-preview editor sync |
| Layout tree/restore | `LayoutWorkflow`, `LayoutTreeWorkflow` | 🔶 Partial (2026-06-12) — `LayoutWorkflow` now owns the named-layout collection (`SavedLayoutNames`, the saved-layout dictionary, and the `Layout {N}` name counter) plus save-as-new/save-to-existing/restore-default/remove orchestration and `BuildNamedLayouts()` for persistence; window geometry capture, dock-tree capture/apply (`CaptureLayoutSnapshot`/`ApplyLayoutSnapshot`/`ReapplyStartupLayout`), and `PersistCurrentLayout`/`CloseFloatingWindows` stay in Main by design (they read/write `Layout`/`IRootDock` and Avalonia window state directly) |
| Options persistence | `ApplicationOptionsWorkflow`, `ApplicationOptionsSnapshot`, `OptionsPersistenceOutcome` | ✅ Done — auto-save debounce, build/validate, save/export/import; the per-option `Apply*` UI projections stay in Main by design (they mutate Avalonia/app state) |
| Scheduled jobs add/remove | `ScheduledJobsWorkflow` | ✅ Done (2026-06-12) — add (interval clamped to `MinIntervalSeconds`), remove (stop + delete + dispose), load with auto-start-on-launch, and job VM disposal; Main keeps the commands and `LeftPanelTab` switch |
| Request tabs + history | `RequestTabsWorkflow`, `RequestHistoryWorkflow` | 🔶 Partial (2026-06-12) — tab add/close (`RequestTabsWorkflow`, `Features/HttpRequest/`) and history load/filter + editor projection (`RequestHistoryWorkflow`, `Features/History/`) extracted; `ApplyActiveRequestTab`, `SaveResponseStateForTab`/`RestoreResponseStateForTab`, and `_lastAppliedRequestTab` stay in Main by design (they read/write Main's `[Reactive]` response-state properties directly, matching the "Apply* UI projections stay in Main" precedent from the options/collections slices) |
| Draft persistence and restore loop | `DraftWorkflow` | ✅ Done (2026-06-13) — `DraftWorkflow` (`Features/Layout/`) owns loading/taking the pending crash-recovery draft, discarding/clearing the persisted draft file, and the periodic auto-save subscription with an injectable `IScheduler` (`TestScheduler`-tested). Main keeps `HasDraftToRestore`/`DraftRestoreMessage` (`[Reactive]`), the `RestoreDraftAsync`/`DiscardDraft` `[ReactiveCommand]`s, and `SaveDraftTickAsync` (captures/restores editor state via `DraftPersistenceService.CaptureFromEditor`/`RestoreToEditor` on the UI thread). Tests: `DraftWorkflowTests`. |
| Collection request → editor projection | `CollectionRequestEditorProjectionWorkflow` | ✅ Done (2026-06-13) — `CollectionRequestEditorProjectionWorkflow` (`Features/Collections/`) owns request-type resolution (`ResolveRequestType`), environment-aware URL resolution/scheme rewriting (`BuildProjection`), merged inherited/manual header projection, content-type/body projection, and the demo-server banner check (`ShouldShowDemoServerBanner`), returning an immutable `CollectionRequestEditorProjection`. Main's `ApplyCollectionRequestToEditor` now just builds the projection and applies it to `_requestEditor`/`IsDemoServerBannerVisible` inside `BeginBulkUpdate`. Tests: `CollectionRequestEditorProjectionWorkflowTests`; existing `DemoServerTests` (`LoadCollectionRequest_*`) continue to cover the integrated behavior. |
| Request body external-edit pipeline | `RequestBodyExternalEditWorkflow` | ✅ Done (2026-06-14) — `RequestBodyExternalEditWorkflow` (`Features/HttpRequest/`) owns temp-file creation (incl. extension detection via `ResponseActionsViewModel.ExtensionFromContentType`/`DetectExtensionFromContent`), `FileSystemWatcher` setup/teardown (cancelling/replacing any previous watcher on re-open), and the debounced read-and-apply loop for external edits. Main's `OpenRequestBodyInExternalEditorAsync` calls `OpenInExternalEditorAsync` with a UI-thread-marshalled apply callback (`Dispatcher.UIThread.InvokeAsync`) and `_tempFiles.Add` for temp-file tracking; `Dispose` delegates to the workflow's `Dispose`. Tests: `RequestBodyExternalEditWorkflowTests`. |
| Dock-tree proportion-reapply helpers | `LayoutTreeWorkflow` | ✅ Done (2026-06-14, later) — promoted the private `FindDockById<T>` lookup and `ReapplyProportionsFromTree` tree-walker used by `ReapplyStartupLayout` to public static methods on `LayoutTreeWorkflow` (`Features/Layout/`), reusing the dock-tree traversal already present there instead of a duplicate copy in Main. `ReapplyStartupLayout` itself remains in Main (dock-tree/window-geometry precedent) and now calls `LayoutTreeWorkflow.ReapplyProportionsFromTree`/`LayoutTreeWorkflow.FindDockById<T>`. Tests: new `LayoutTreeWorkflowTests` (headless). |
| Inherited-header preview comparison helpers | `CollectionInheritedHeadersWorkflow` | ✅ Done (2026-06-14, later 2) — promoted the private `IsInheritedHeader` and `HasManualHeaderOverride` pure helpers used by `AppendInheritedHeadersWithoutManualOverrides` to public static methods on `CollectionInheritedHeadersWorkflow` (`Features/Collections/`), alongside the existing `BuildHeaders`/`MergeCollectionAndRequestHeaders`/`HeadersEqual`. `AppendInheritedHeadersWithoutManualOverrides`/`SyncActiveCollectionRequestInheritedHeaders` remain in Main (live-preview editor sync precedent) and now call `CollectionInheritedHeadersWorkflow.IsInheritedHeader`/`HasManualHeaderOverride`. Tests: new cases in `CollectionInheritedHeadersWorkflowTests`. |
| Response-state byte reuse helper | `RequestTabsWorkflow` | ✅ Done (2026-06-14, later 3) — moved the private `GetResponseStateBytes` (reuses the backing array of `ReadOnlyMemory<byte>` when it wraps a whole array) used by `RestoreResponseStateForTab` to a public static method on `RequestTabsWorkflow` (`Features/HttpRequest/`), alongside the tab add/close lifecycle it already owns. Tests: new cases in `RequestTabsWorkflowTests`. |
| Temp-file cleanup on dispose | `ResponseActionsViewModel` | ✅ Done (2026-06-14, later 3) — moved the best-effort temp-file delete loop from `MainWindowViewModel.Dispose` to `ResponseActionsViewModel.DeleteTempFiles` (`Features/HttpRequest/`), next to the other temp-file helpers (`OpenWithShell`) that slice already owns. `Dispose` now calls `ResponseActionsViewModel.DeleteTempFiles(_tempFiles)`. Tests: new cases in `ResponseActionsViewModelTests`. |
| Manual send outcome handling | `MainWindowViewModel.ApplyManualRequestOutcome` (private) | ✅ Done (2026-06-14, later 3) — deduplicated the identical failure/success handling in `SendHttpRequestAsync` and `SendGraphQlRequestAsync` into a single private helper. Stays in Main per the "Apply* UI projections stay in Main" precedent (writes `ErrorMessage`/`ResponseStatusCode`/etc. and calls `ApplyHttpResponseProjection`/`_cookieJarViewModel.RefreshCookies`). Covered by existing `MainWindowUiTests`/`ResponseShortcutsTests` send-path coverage. |
| Default content-type selection resolution | `ApplicationOptionsWorkflow` | ✅ Done (2026-06-14, later 3) — promoted the pure decision logic from `SyncDefaultContentTypeSelection` (known option vs. "Custom..." fallback) to `ApplicationOptionsWorkflow.ResolveDefaultContentTypeSelection` (`Features/Options/`), alongside `BuildOptions`. `SyncDefaultContentTypeSelection` stays in Main (writes `SelectedDefaultContentTypeOption`/`CustomDefaultContentType`) and now applies the resolved tuple. Tests: new cases in `ApplicationOptionsWorkflowTests`. |
| Collection-request matching lookup | `CollectionInheritedHeadersWorkflow` | ✅ Done (2026-06-14, later 4) — moved the method/path/name request-matching lookup from `TryGetActiveCollectionRequestContext` to `CollectionInheritedHeadersWorkflow.FindMatchingRequest(Collection, method, path, name)` (`Features/Collections/`), alongside the other header/request helpers. Tests: new cases in `CollectionInheritedHeadersWorkflowTests`. |
| Request-URL overwrite decision | `ApplicationOptionsWorkflow` | ✅ Done (2026-06-14, later 4) — moved the "should the loaded options overwrite the current request URL" decision from `ApplyOptions` to `ApplicationOptionsWorkflow.ShouldUpdateRequestUrl`. `ApplyOptions` stays in Main (writes `_requestEditor.RequestUrl`). Tests: new cases in `ApplicationOptionsWorkflowTests`. |
| Theme-variant resolution | `ApplicationOptionsWorkflow` | ✅ Done (2026-06-14, later 4) — moved the theme-string → `ThemeVariant` mapping from `ApplyThemeOption` to `ApplicationOptionsWorkflow.ResolveThemeVariant`. `ApplyThemeOption` stays in Main (writes `Application.Current.RequestedThemeVariant`). Tests: new cases in `ApplicationOptionsWorkflowTests`. |
| Font-family fallback resolution | `ApplicationOptionsWorkflow` | ✅ Done (2026-06-14, later 4) — moved the comma-separated font-family fallback resolution from `ApplyUiFontFamily` to `ApplicationOptionsWorkflow.ResolveFontFamily`. `ApplyUiFontFamily` stays in Main (writes `Application.Current.Resources["AppFontFamily"]`). Tests: new cases in `ApplicationOptionsWorkflowTests`. |
| Font-size parse-with-fallback | `ApplicationOptionsWorkflow` | ✅ Done (2026-06-14, later 4) — moved the `UiFontSize` parse-with-fallback logic to `ApplicationOptionsWorkflow.ParseFontSize`. Tests: new cases in `ApplicationOptionsWorkflowTests`. |
| Existing-tab lookup for a collection request | `RequestTabsWorkflow` | ✅ Done (2026-06-14, later 5) — moved the `RequestTabs` scan from `TryActivateExistingCollectionRequestTab` to `RequestTabsWorkflow.FindMatchingTab(tabs, collectionId, method, path, name)` (`Features/HttpRequest/`), alongside `AddTab`/`CloseTab`. `TryActivateExistingCollectionRequestTab` stays in Main (writes `ActiveRequestTab`). Tests: new cases in `RequestTabsWorkflowTests`. |
| Inherited-header property-change merge | `CollectionInheritedHeadersWorkflow` | ✅ Done (2026-06-14, later 5) — moved the `CollectionInheritedHeaders` item `PropertyChangedObservable` merge from `ObserveCollectionInheritedHeaderPropertyChanges` to `CollectionInheritedHeadersWorkflow.ObservePropertyChanges(IEnumerable<RequestHeaderViewModel>)` (`Features/Collections/`), alongside the other header helpers. `ObserveCollectionInheritedHeaderPropertyChanges` is now a one-line delegation. Tests: new cases in `CollectionInheritedHeadersWorkflowTests`. |
| Selected-collection item/header projection | `CollectionFilterWorkflow`, `CollectionInheritedHeadersWorkflow` | ✅ Done (2026-06-14, later 5) — moved the `collection.Requests` → `CollectionItemViewModel` loop from `ApplySelectedCollection` to `CollectionFilterWorkflow.BuildCollectionItems(Collection)`, and the `collection.Headers` → `RequestHeaderViewModel` loop to `CollectionInheritedHeadersWorkflow.BuildHeaderViewModels(IReadOnlyList<RequestHeader>?)`. `ApplySelectedCollection` stays in Main (mutates `CollectionItems`/`CollectionInheritedHeaders`) and now just iterates the built lists. Tests: new cases in `CollectionFilterWorkflowTests`/`CollectionInheritedHeadersWorkflowTests`. |
| Collection-request method resolution | `CollectionRequestEditorProjectionWorkflow` | ✅ Done (2026-06-14, later 6) — moved the "is this an HTTP request, and if so which method to apply" decision from `ApplyCollectionRequestToEditor` into a new `Method` field on `CollectionRequestEditorProjection`, computed by `BuildProjection` (`item.Method` when `RequestType.Http`, otherwise `null`). `ApplyCollectionRequestToEditor` now just does `if (projection.Method is { } method) { _requestEditor.SelectedMethod = method; }`. Tests: new assertions in `CollectionRequestEditorProjectionWorkflowTests`. |

<a id="remaining-slices--ordered-plan-2026-06-11"></a>
#### Remaining slices — ordered plan (2026-06-11)

Execute one slice per PR, in this order (lowest coupling first), following the Workflow/Coordinator convention above:

1. ~~**Options workflow**~~ ✅ Done — `ApplicationOptionsWorkflow` (`Features/Options/`) owns the debounced auto-save pipeline (injectable `IScheduler`, `TestScheduler`-tested), nesting auto-save suppression scopes, snapshot-based `BuildOptions` validation, and the save/export/import persistence flows with `OptionsPersistenceOutcome` results. Main keeps the option `[Reactive]` properties, the per-option `Apply*` UI projections (theme/font/TLS mutate Avalonia and service state), and the file pickers. Tests: `ApplicationOptionsWorkflowTests` + `ApplicationOptionsWorkflowPersistenceTests`.
2. ~~**Collections UI workflows**~~ ✅ Done (2026-06-12) — `CollectionInheritedHeadersWorkflow` owns the inherited-headers debounced auto-save (injectable `IScheduler`, `TestScheduler`-tested), suppression scopes, pending/flush tracking, snapshot persistence, and the shared `BuildHeaders`/`MergeCollectionAndRequestHeaders`/`HeadersEqual` helpers; `CollectionFilterWorkflow` owns filter/sort/group with expansion-state preservation; import/delete moved into `CollectionsManagementCoordinator` (`ImportCollectionOutcome`/`DeleteCollectionOutcome`). Main keeps the `[Reactive]` form-visibility state (per the established convention), the dispatcher marshalling, the file picker, and the live-preview editor sync (`SyncActiveCollectionRequestInheritedHeaders` projects onto the request editor). DynamicData was considered and deliberately not adopted — the extracted pipeline stays behavior-identical; revisit if the history slice adopts it. Tests: `CollectionInheritedHeadersWorkflowTests`, `CollectionFilterWorkflowTests`, `CollectionsManagementCoordinatorTests`.
3. ~~**Scheduled jobs workflow**~~ ✅ Done (2026-06-12) — `ScheduledJobsWorkflow` (`Features/ScheduledJobs/`) owns the `Jobs` collection and its lifecycle: `AddJob` (interval clamped to the moved `MinIntervalSeconds` constant), `RemoveJobAsync` (stop + repository delete + dispose), `LoadJobsAsync` (replace + optional auto-start on launch), and disposal of job view models. Main keeps the `[ReactiveCommand]` entry points, the `ScheduledJobs` pass-through property for XAML, and the `LeftPanelTab` switch. Tests: `ScheduledJobsWorkflowTests` (uses a never-advanced `TestScheduler` on `ScheduledJobService` so started jobs never tick).
4. ~~**Request tabs + history**~~ 🔶 Partial (2026-06-12) — `RequestTabsWorkflow` (`Features/HttpRequest/`) owns the `Tabs` collection: `AddTab` (wrap an editor in a new tab) and `CloseTab` (remove + dispose, keeping at least one tab, returning the tab that should become active). `RequestHistoryWorkflow` (`Features/History/`) owns the `History` collection: `LoadAsync` (reload from the repository, ordered by `CreatedAtUtc` descending), `ApplyFilter` (name/URL/method search, preserving item identity for unchanged entries), and `BuildEditorProjection` (maps a `RequestHistoryEntry` to the editor fields `LoadHistoryRequest` applies). Main keeps the `RequestTabs`/`History` pass-through properties for XAML, the `[ReactiveCommand]` entry points, `ActiveRequestTab` and its `ApplyActiveRequestTab` projection (editor swap + `PrimaryActionLabel`/`RequestEditor` notifications), and `SaveResponseStateForTab`/`RestoreResponseStateForTab` (per-tab response-state snapshotting against Main's `[Reactive]` response properties) — these remain in Main per the "Apply* UI projections stay in Main" precedent. Tests: `RequestTabsWorkflowTests`, `RequestHistoryWorkflowTests`.
5. ~~**Layout management**~~ 🔶 Partial (2026-06-12) — `LayoutWorkflow` (`Features/Layout/`) now owns the named-layout collection: `SavedLayoutNames` (ordered alphabetically), the saved-layout dictionary, and the `Layout {N}` name counter; `LoadFromOptions` (replaces `ApplyLayoutOptions`'s dictionary/name bookkeeping), `TryGetLayout`, `SaveLayoutAsNew`, `SaveLayoutToExisting`, `RestoreDefaultLayout`, `RemoveLayout`, and `BuildNamedLayouts` (for persistence). Main keeps `SelectedLayoutName` (`[Reactive]`, bound to the layout combo box), the `[ReactiveCommand]` entry points, and the dock-tree/geometry pipeline (`CaptureLayoutSnapshot`/`ApplyLayoutSnapshot`/`ReapplyStartupLayout`/`PersistCurrentLayout`/`SetWindowGeometry`/`CloseFloatingWindows`) since these read/write `Layout` (`IRootDock`) and Avalonia window state directly — consistent with the "Apply* UI projections stay in Main" precedent. Tests: `LayoutWorkflowTests`.
6. ~~**Phase 3 delegation cleanup**~~ ✅ Done (2026-06-13) — `ResponseActionsViewModel` is now `sealed partial class ResponseActionsViewModel : ReactiveViewModelBase` and its six action methods (`CopyResponseBodyAsync`, `SaveResponseBodyAsFileAsync`, `OpenResponseBodyInExternalEditorAsync`, `SaveBinaryResponseAndOpenAsync`, `CopyHistoryItemAsCurlAsync`, `CopyCurrentRequestAsCurlAsync`) are private `[ReactiveCommand]`-decorated members exposing `*Command` properties directly. `MainWindowViewModel`'s pass-through `[ReactiveCommand]` wrappers and the `TryGetSaveableResponseContent`/`ExtensionFromContentType`/`DetectExtensionFromContent` static delegations are removed; XAML (`ResponseView.axaml`, `EmbeddedResponseView.axaml`, `LeftPanelView.axaml`) and code-behind (`ResponseView.axaml.cs`, `EmbeddedResponseView.axaml.cs`, `RequestView.axaml.cs`) bind/call `App.ResponseActions.*Command` and `ResponseActionsViewModel.ExtensionFromContentType`/`DetectExtensionFromContent` directly. `IResponseActionsContext` (the explicit-interface state/IO boundary implemented by Main) is retained — it is the dependency-inversion contract, not delegation. Main now disposes `_responseActions` in `Dispose(bool)`. Tests updated in `ResponseActionsViewModelTests` and `ResponseShortcutsTests`.
7. **DockFactory feature registrations** — only after the slices above: replace the `MainWindowViewModel` constructor dependency in `DockFactory` with per-feature dockable registrations (step 2 in `clean-feature-separation.md`). Deliberately deferred so registrations can point at clean feature VMs.

> **Status check (2026-06-14, later 6):** Slices 1–6 above are now done or partial-by-design, so this is the only remaining item in the ordered plan. Scoping it out: `DockFactory.CreateLayout()` constructs four dockable VMs — `LeftPanelViewModel`, `LogPanelViewModel` (`Features/Logging/`), `RequestViewModel` (`Features/HttpRequest/`), and `LayoutManagementViewModel` (`Features/Layout/`) — each a ~20-line "thin proxy" whose entire surface is a single `public MainWindowViewModel App { get; }` property (the "Limited reuse boundaries" finding in `clean-feature-separation.md`). Their corresponding AXAML views (`LeftPanelView.axaml`, `RequestView.axaml`, `LogPanelView.axaml`, `LayoutManagementView.axaml`) bind through `App.*` to dozens of `MainWindowViewModel` members each (commands, `[Reactive]` properties, sub-VMs). Replacing `App` with narrow per-feature interfaces means rewriting every one of those binding paths across four AXAML files and re-verifying the UI visually — a multi-PR binding migration with UI regression testing, not a pure-C# extraction like slices 1–6. It cannot be done safely headless (this environment has no display for Avalonia). Recommendation: treat as its own dedicated effort, one dockable VM/view at a time, with `./scripts/take-screenshots.sh` before/after each.

#### Remaining work beyond this plan

With the Workflow/Coordinator extraction backlog (items 1–6) essentially exhausted — further rounds now find only marginal pure-helper moves like the `Method` projection above — the two substantive items left for "split MainWindowViewModel into separate features" are:

- **Item 7 above** (DockFactory feature registrations) — large, UI-binding-migration risk, needs visual verification.
- **Test split plan** (below) — `MainWindowUiTests.cs` (~2,430 lines) and `RequestEditorViewModelTests.cs` (~738 lines) have not yet been split into the focused per-feature test classes described in [Test split plan](#test-split-plan). This is lower-risk (compile + `dotnet test` verified, no UI binding changes) and can be done incrementally, one behavior area per PR.

#### Phase 2 Slice 1 — Response actions ✅ Implemented

> Implemented in this PR — `src/Arbor.HttpClient.Desktop/Features/HttpRequest/ResponseActionsViewModel.cs`

- `ResponseActionsViewModel` created, owning:
  - `CopyResponseBodyAsync`, `SaveResponseBodyAsFileAsync`, `OpenResponseBodyInExternalEditorAsync`
  - `SaveBinaryResponseAndOpenAsync`, `CopyHistoryItemAsCurlAsync`, `CopyCurrentRequestAsCurlAsync`
  - `TryGetSaveableResponseContent`, `ExtensionFromContentType`, `DetectExtensionFromContent`, `OpenWithShell`
- `MainWindowViewModel` implements `IResponseActionsContext` and delegates all response-action `[RelayCommand]` methods
- Static helpers kept on `MainWindowViewModel` via delegation for backward-compat
- 22 new focused tests in `ResponseActionsViewModelTests.cs`

**Exit criteria per slice:** behavior parity ✅, targeted tests pass ✅, no unrelated file churn ✅

### Phase 3 — Remove temporary delegation surface

- Remove obsolete pass-through members from `MainWindowViewModel` after XAML and dependent code bind directly to feature VMs.
- Keep only composition/orchestration concerns in Main.

**Exit criteria:** Main is orchestration-only; no duplicate command paths.

**Status (2026-06-13):** Response-actions delegation removed (see slice 6 above). Other slices marked "Partial" (request tabs/history, layout) intentionally keep `Apply*`/projection logic in Main per the established precedent and are not delegation in the same sense — no further Phase 3 cleanup is pending for those slices.

### Phase 4 — Hardening and guardrails

- Add architecture checks to PR review habits:
  - “No new feature logic in MainWindowViewModel.”
  - “New feature requires at least one focused unit test not requiring full UI runtime.”
- Keep feature-folder placement strict.

**Exit criteria:** new feature additions are mostly isolated to one feature folder plus composition wiring.

## Test split plan

Current large classes should be incrementally split by behavior area.

### MainWindow UI tests

Split `MainWindowUiTests` into focused classes, for example:

- `MainWindowLayoutUiTests`
- `MainWindowCollectionsUiTests`
- `MainWindowScheduledJobsUiTests`
- `MainWindowRequestExecutionUiTests`
- `MainWindowOptionsUiTests`

### Request editor tests

Split `RequestEditorViewModelTests` by behavior area, for example:

- `RequestEditorHeadersTests`
- `RequestEditorQueryParametersTests`
- `RequestEditorBodyFormattingTests`
- `RequestEditorDraftPersistenceTests`

### Test design rules during split

- Keep test classes small and single-purpose.
- Prefer many focused test classes over one giant class.
- Keep integration tests explicitly categorized and isolated from pure unit tests.

## Communication pattern options between modules

> **Superseded note (2026-06-11):** the table below predates the full ReactiveUI/Rx.NET migration (completed in PRs #218–#220). The original recommendation assumed CommunityToolkit.Mvvm as the baseline; that constraint no longer exists. The table is kept for historical context — current guidance follows in the next section.

| Option | Description | Pros | Cons | Impact | Recommendation |
|---|---|---|---|---|---|
| A. Direct interface calls (default) | Feature VM/coordinator calls another via small interface contracts | Simple, explicit, easy to debug, minimal infrastructure | Can grow coupling if interfaces become broad | Low migration cost, low risk | **Start here** |
| B. Domain events / event aggregator | Features publish events (`RequestSent`, `EnvironmentChanged`) consumed by subscribers | Decouples producers/consumers, good for cross-feature notifications | Harder tracing, risk of hidden flows, ordering complexity | Medium cost, medium risk | Use only where multiple subscribers exist |
| C. Shared state store (single source of truth) | Central immutable-ish state + reducers/actions | Predictable state transitions, easier time-travel/debug tooling | Higher conceptual overhead, boilerplate, broad migration | High cost, medium risk | Not needed now |
| D. Mediator/command bus | Commands routed through mediator handlers | Strong separation, extensible for plugins/modules | Indirection, can hide ownership, overkill early | Medium-high cost | Re-evaluate after 1–2 successful extractions |
| E. RX.NET observable streams | Feature VMs expose `IObservable<T>` endpoints; consumers subscribe with composable operators | Composable fan-out (throttle, filter, combine), `IScheduler` enables time-travel unit tests, pairs with DynamicData for collection transforms | Medium-high learning curve, subscription lifetime management, debugging stream chains | Low if additive alongside CommunityToolkit.Mvvm; high if replacing it | Use selectively for time-dependent features and cross-feature notifications once ≥ 2 features are extracted; see [`docs/architecture/rx-reactive-evaluation.md`](rx-reactive-evaluation.md) |

### Current guidance (post-ReactiveUI migration, 2026-06-11)

ReactiveUI + Rx.NET is now the solution-wide MVVM stack, so the A-vs-E trade-off has collapsed: both are idiomatic and already in use. Pick per interaction shape:

- **Command-style flows (request/response with a result)** — keep **Option A as direct Workflow/Coordinator calls returning immutable outcome records** (the established convention above). This is explicit, debuggable, and already proven by `ManualHttpRequestCoordinator`/`ManualHttpRequestOutcome`. Do not convert these to streams; a `Task<TOutcome>` is the right shape for one-shot operations.
- **Notifications and derived state (fan-out, no reply expected)** — use **Option E**: the producing workflow/VM exposes an `IObservable<T>` (Subject-backed or `WhenAnyValue`-derived); consumers subscribe at the composition root with `.DisposeWith(Disposables)`. Already in use for the options-autosave, history-filter, and collection-search subjects in `MainWindowViewModel`; when extracting those slices, move the subject into the owning workflow and expose it as `IObservable<T>`.
- **Time-dependent behavior (debounce/throttle/timers)** — model as Rx pipelines with an injectable `IScheduler` so tests can use `TestScheduler` (`Microsoft.Reactive.Testing` is already pinned; see `docs/suggestions/SchedulerInjection.md`).
- **Dialogs and file pickers** — prefer ReactiveUI `Interaction<TInput, TOutput>` over the current `Action` delegates (`OpenAboutWindowAction` etc.) as slices are extracted; this is the planned follow-up from the migration (plan §3.3/§3.5).
- **Collection filtering/sorting** — use DynamicData (`SourceList` + `Filter`/`Sort`/`Bind`) when extracting the collections and history slices (see `docs/suggestions/DynamicDataCollections.md`).
- **Rejected: Option B (event aggregator) and `ReactiveUI.MessageBus`** — ReactiveUI's own documentation discourages MessageBus as a service-locator-style pattern that hides flows; explicit `IObservable<T>` endpoints on constructor-injected dependencies provide the same decoupling with visible ownership. **Option D (mediator)** stays rejected — the Workflow/Coordinator extractions have not produced composition friction that would justify it. **Option C (state store)** remains unnecessary.

## Risk controls

- Migrate in small PRs with one slice per PR.
- Preserve existing command/property names temporarily via delegation to reduce UI break risk.
- Validate each extraction with targeted tests first, then full suite.
- Avoid simultaneous refactor of multiple feature slices.

## Success metrics

- `MainWindowViewModel` reduced substantially and mainly orchestration/composition.
- New feature PRs touch primarily one feature folder + minimal composition wiring.
- Tests are split into smaller specialized classes by feature area.
- Cross-feature communication is explicit and intentional.

## Definition of done for this issue

- ✅ This plan is persisted in the repository.
- ✅ Phase 1 (contracts) and Phase 2 Slice 1 (response actions) implemented in the first PR.
- Future implementation PRs should reference this document and execute the remaining slices incrementally.
