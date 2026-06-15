# Clean feature separation

This document answers the architecture questions raised in [issue #33](https://github.com/niklaslundberg/Arbor.HttpClient/issues/33) and provides an ordered task list for keeping the UI modular, testable, and easy to extend.

## Scope

- Are view models separated in a scalable way?
- Can components be reused?
- Can new features be added without touching existing files (to a great extent)?
- Is the code easy to test?
- What other architectural questions should we track continuously?

## Findings

> **Update 2026-06-12:** extraction is well underway. Request execution (HTTP/GraphQL/WebSocket/SSE), response projection, response actions, the demo-server lifecycle, options persistence (auto-save/save/export/import via `ApplicationOptionsWorkflow`), the collections workflows (management incl. import/delete via `CollectionsManagementCoordinator`, inherited-headers autosave via `CollectionInheritedHeadersWorkflow`, filter/sort/group via `CollectionFilterWorkflow`), the scheduled-jobs lifecycle (add/remove/load incl. auto-start via `ScheduledJobsWorkflow`), the request-tab list and history load/filter (`RequestTabsWorkflow`, `RequestHistoryWorkflow`), and the named-layout collection (`SavedLayoutNames`, save-as-new/save-to-existing/restore-default/remove via `LayoutWorkflow`) now live in feature-scoped Workflow/Coordinator classes; layout is partially extracted. `MainWindowViewModel` still owns the per-option UI projections, the dock-tree/window-geometry layout pipeline, active-tab/response-state projection, and history-entry-to-editor application. Current status and the ordered forward plan live in [`mainwindowviewmodel-split-plan.md`](mainwindowviewmodel-split-plan.md). The findings below describe the original state and are kept for context.
>
> **Update 2026-06-13:** Phase 3 delegation cleanup completed for the response-actions slice — `ResponseActionsViewModel` now exposes `[ReactiveCommand]`-generated command properties directly (`ResponseActions.CopyResponseBodyCommand`, etc.), and `MainWindowViewModel`'s pass-through commands and static delegation helpers for that slice were removed. The next remaining step toward "new feature requires touching only one folder" is `DockFactory` feature registrations (step 7 in the split plan), still deferred until the request-tabs/history and layout slices are fully closed out.
>
> **Update 2026-06-13 (later):** Extracted the collection-request → editor projection into `CollectionRequestEditorProjectionWorkflow` (`Features/Collections/`) — request-type resolution, environment-aware URL/scheme resolution, merged inherited/manual header projection, content-type/body projection, and the demo-server banner check are now a pure, independently-tested projection builder. `MainWindowViewModel.ApplyCollectionRequestToEditor` applies the resulting `CollectionRequestEditorProjection` to the editor inside `BeginBulkUpdate`. `MainWindowViewModel` is now about 2,782 lines.
>
> **Update 2026-06-14:** Extracted the "open request body in external editor" pipeline into `RequestBodyExternalEditWorkflow` (`Features/HttpRequest/`) — temp-file creation (incl. extension detection from content-type/content), `FileSystemWatcher` setup/teardown, and the debounced external-edit-apply loop are now an independently-tested, UI-agnostic class. `MainWindowViewModel.OpenRequestBodyInExternalEditorAsync` now just calls the workflow and passes a UI-thread-marshalled apply callback; `Dispose` delegates teardown to the workflow. `MainWindowViewModel` is now about 2,710 lines.
>
> **Update 2026-06-14 (later):** Moved the dock-tree proportion-reapply helpers used by `ReapplyStartupLayout` — `ReapplyProportionsFromTree` and the generic `FindDockById<T>` lookup — into `LayoutTreeWorkflow` (`Features/Layout/`) as public static methods, removing a duplicate tree-walking implementation from `MainWindowViewModel` (an equivalent `FindDockById<T>`/`ApplyDockTreeInPlace` pair already existed in `LayoutTreeWorkflow`). `ReapplyStartupLayout` itself stays in Main per the dock-tree/window-geometry precedent, but now delegates its tree-walking to `LayoutTreeWorkflow`. Tests: new `LayoutTreeWorkflowTests` (headless, no Avalonia runtime) plus the existing `MainWindowUiTests.ReapplyStartupLayout_*` coverage. `MainWindowViewModel` is now about 2,666 lines.
>
> **Update 2026-06-14 (later 2):** Moved the pure header-comparison helpers used by `AppendInheritedHeadersWithoutManualOverrides` — `IsInheritedHeader` and `HasManualHeaderOverride` — into `CollectionInheritedHeadersWorkflow` (`Features/Collections/`) as public static methods, alongside the existing `BuildHeaders`/`MergeCollectionAndRequestHeaders`/`HeadersEqual` helpers. `AppendInheritedHeadersWithoutManualOverrides` itself stays in Main (it mutates `_requestEditor.RequestHeaders` as part of the live-preview sync), but now calls `CollectionInheritedHeadersWorkflow.IsInheritedHeader`/`HasManualHeaderOverride`. Tests: new cases in `CollectionInheritedHeadersWorkflowTests`. `MainWindowViewModel` is now about 2,658 lines.
>
> **Update 2026-06-14 (later 3):** Five small cleanups continuing the same extraction pattern:
> 1. Removed the dead `MainWindowViewModel.FormatElapsedMilliseconds`/`FormatByteSize` pass-through statics — both already just delegated to `HttpResponseProjectionWorkflow`, which the remaining callers (`ResponseStatusFormatTests`) now call directly.
> 2. Moved `GetResponseStateBytes` (response-state-snapshot byte-array reuse helper used by `RestoreResponseStateForTab`) from a private static on `MainWindowViewModel` to a public static on `RequestTabsWorkflow` (`Features/HttpRequest/`), alongside the tab add/close lifecycle it already owns. Tests: new cases in `RequestTabsWorkflowTests`.
> 3. Extracted the best-effort temp-file cleanup loop from `MainWindowViewModel.Dispose` into `ResponseActionsViewModel.DeleteTempFiles` (`Features/HttpRequest/`), next to the other temp-file helpers (`OpenWithShell`) that slice already owns. Tests: new cases in `ResponseActionsViewModelTests`.
> 4. Deduplicated the identical outcome-handling blocks in `SendHttpRequestAsync` and `SendGraphQlRequestAsync` (set `ErrorMessage`/clear response metadata on failure, otherwise project the response and refresh the cookie jar) into a single private `ApplyManualRequestOutcome` helper on `MainWindowViewModel`. This stays in Main per the "Apply* UI projections stay in Main" precedent since it writes directly to Main's `[Reactive]` properties; existing `MainWindowUiTests`/`ResponseShortcutsTests` coverage of both send paths exercises it.
> 5. Moved the pure decision logic from `SyncDefaultContentTypeSelection` — whether a content-type value matches a known picker option or falls back to the "Custom..." option — into `ApplicationOptionsWorkflow.ResolveDefaultContentTypeSelection` (`Features/Options/`) as a public static method, alongside `BuildOptions`. `SyncDefaultContentTypeSelection` itself stays in Main (it writes `SelectedDefaultContentTypeOption`/`CustomDefaultContentType`) but now just applies the resolved tuple. Tests: new cases in `ApplicationOptionsWorkflowTests`.
>
> `MainWindowViewModel` is now about 2,603 lines.
>
> **Update 2026-06-14 (later 4):** Five more small pure-helper extractions continuing the same pattern:
> 1. Moved the request-matching lookup from `TryGetActiveCollectionRequestContext` into `CollectionInheritedHeadersWorkflow.FindMatchingRequest(Collection, method, path, name)` (`Features/Collections/`), alongside the other header/request helpers. Tests: new cases in `CollectionInheritedHeadersWorkflowTests`.
> 2. Moved the "should the loaded options overwrite the current request URL" decision from `ApplyOptions` into `ApplicationOptionsWorkflow.ShouldUpdateRequestUrl` (`Features/Options/`). Tests: new cases in `ApplicationOptionsWorkflowTests`.
> 3. Moved the theme-string → `ThemeVariant` mapping from `ApplyThemeOption` into `ApplicationOptionsWorkflow.ResolveThemeVariant`. Tests: new cases in `ApplicationOptionsWorkflowTests`.
> 4. Moved the comma-separated font-family fallback resolution from `ApplyUiFontFamily` into `ApplicationOptionsWorkflow.ResolveFontFamily`. Tests: new cases in `ApplicationOptionsWorkflowTests`.
> 5. Moved the `UiFontSize` parse-with-fallback logic into `ApplicationOptionsWorkflow.ParseFontSize`. Tests: new cases in `ApplicationOptionsWorkflowTests`.
>
> All five `Apply*`/sync methods stay in Main (they read/write `[Reactive]` properties and `Application.Current`), but now delegate their pure decision logic to the workflow classes. The now-unused `Avalonia.Media`/`Avalonia.Styling` usings were removed from `MainWindowViewModel`. `MainWindowViewModel` is now about 2,585 lines.
>
> **Update 2026-06-14 (later 5/6):** Continuing the same pattern: moved the existing-tab lookup for a collection request into `RequestTabsWorkflow.FindMatchingTab`, the inherited-header property-change merge into `CollectionInheritedHeadersWorkflow.ObservePropertyChanges`, the `ApplySelectedCollection` projection loops into `CollectionFilterWorkflow.BuildCollectionItems`/`CollectionInheritedHeadersWorkflow.BuildHeaderViewModels`, and — most recently — the "is this an HTTP request, and which method to apply" decision from `ApplyCollectionRequestToEditor` into a new `Method` field on `CollectionRequestEditorProjectionWorkflow.BuildProjection`'s result. `MainWindowViewModel` is about 2,567 lines.
>
> **Status check (2026-06-14, later 6):** The Workflow/Coordinator pure-helper extraction backlog (`mainwindowviewmodel-split-plan.md` items 1–6) is now done or partial-by-design; further rounds find only marginal moves like the one above. The two substantive items remaining for "new feature requires touching only one folder" are (a) **`DockFactory` feature registrations** (step 7/2) — found to require migrating AXAML binding paths across four "thin proxy" dockable VMs (`LeftPanelViewModel`, `LogPanelViewModel`, `RequestViewModel`, `LayoutManagementViewModel`), each of which exposes only `App` (the full `MainWindowViewModel`) — a multi-PR UI-binding migration needing visual regression testing that can't be done safely headless; and (b) the **test split plan** (`MainWindowUiTests.cs` ~2,430 lines, `RequestEditorViewModelTests.cs` ~738 lines, not yet split into focused per-area classes) — lower-risk and `dotnet test`-verifiable. See the "Remaining work beyond this plan" section of `mainwindowviewmodel-split-plan.md` for details.
>
> **Update 2026-06-14 (later 7):** Completed the test split plan from (b) above. `MainWindowUiTests.cs` (2,430 lines, 47 tests) is now `MainWindowLayoutUiTests`, `MainWindowCollectionsUiTests`, `MainWindowScheduledJobsUiTests`, `MainWindowRequestExecutionUiTests`, and `MainWindowOptionsUiTests`, plus a shared `UiTestHelpers` internal static class for the helpers (`WaitForUiThreadAsync`, `VerifyTabRealized`, `FindDockById<T>`, `RedirectTestServer`, `AsyncStubHttpMessageHandler`) used across them. `RequestEditorViewModelTests.cs` (738 lines, 53 tests) is now `RequestEditorQueryParametersTests`, `RequestEditorHeadersTests`, `RequestEditorBodyFormattingTests`, and `RequestEditorDraftPersistenceTests`, plus a shared `RequestEditorTestHelpers` class for `CreateEditor`. No test bodies changed; `dotnet test Arbor.HttpClient.slnx` still reports 861 tests, all passing. The only item remaining from the split-plan backlog is `DockFactory` feature registrations (item 7 / (a) above).
>
> **Update 2026-06-14 (later 8):** Two more marginal pure-helper moves continuing the same pattern:
> 1. Moved the `CollectionGroups` expansion-state capture (`GroupKey` → `IsExpanded`, case-insensitive) from `ApplySelectedCollection`'s sibling `ApplyCollectionFilter` into `CollectionFilterWorkflow.CaptureExpansionState(IEnumerable<CollectionGroupViewModel>)` (`Features/Collections/`), alongside `BuildCollectionItems`/`Apply`. `ApplyCollectionFilter` now just calls it before `_collectionFilterWorkflow.Apply`. Tests: new cases in `CollectionFilterWorkflowTests`.
> 2. Moved the "which request-header rows were added/edited manually" filter (`!IsInherited`) from `SyncActiveCollectionRequestInheritedHeaders` into `CollectionInheritedHeadersWorkflow.SelectManualHeaders(IEnumerable<RequestHeaderViewModel>)`, alongside the other header helpers. `SyncActiveCollectionRequestInheritedHeaders` now just calls it. Tests: new cases in `CollectionInheritedHeadersWorkflowTests`.
>
> `MainWindowViewModel` is now about 2,562 lines.
>
> **Update 2026-06-14 (later 9):** Started the DockFactory feature-registration work (step 2 below / split-plan item 7), decoupling two of the four dockable VMs from `MainWindowViewModel`. `LogPanelViewModel` now takes `LogWindowViewModel` directly (its view only ever bound `App.LogWindowViewModel.*`, now `Logs.*`); `LayoutManagementViewModel` now takes the narrow `ILayoutManagementContext` interface (`Features/Layout/`) implemented by `MainWindowViewModel`, instead of the whole main VM. `DockFactory` receives the `LogWindowViewModel` as a constructor parameter. `LeftPanelViewModel` and `RequestViewModel` (the ~40- and ~70-binding views) remain on the `App`/`MainWindowViewModel` proxy by design. Verified headlessly: the `Category=Screenshots` E2E tests render both dockables through the real `DockFactory` + compiled bindings, and `log-panel.png`/`layout-panel.png` are byte-identical before/after. New tests: `DockableViewModelDecouplingTests`.
>
> **Update 2026-06-14 (later 10):** Decoupled the third dockable, `LeftPanelViewModel` (Explorer), via the `ILeftPanelContext` interface (`Features/Collections/`) — the History/Collections/Scheduled-Jobs tab state, collection management commands/forms, inherited-headers editor, search/sort/display state, scheduled-jobs list, and the `RequestEditor`/`ResponseActions` sub-VMs the item templates use. `MainWindowViewModel` implements it (commands and `ActiveEnvironmentVariables` via explicit interface implementation). The shared `VariableTextBox.AppViewModel` was also narrowed from `MainWindowViewModel?` to `IVariableAutoCompleteHost?` (`Features/Variables/`, just `ActiveEnvironmentVariables`) so the control no longer depends on the whole VM. Only `RequestViewModel` still takes the full `MainWindowViewModel`. Verified headlessly: `collections-panel.png`/`collections-panel-tree.png`/`scheduled-jobs.png` and the request-editor `variables-*` showcases are byte-identical before/after. New tests in `DockableViewModelDecouplingTests`.
>
> **Update 2026-06-14 (later 11):** Decoupled the fourth and last dockable, `RequestViewModel` (the Request document, including its integrated `EmbeddedResponseView` response panel), via the `IRequestPanelContext` interface (`Features/HttpRequest/`) — the request-tab strip, the per-tab `RequestEditorViewModel`, the GraphQL/WebSocket/SSE/scripting sub-VMs, the primary-action/demo-server state, the full response-state projection the embedded response panel renders, and the editor font settings. Both view code-behinds had their app-VM field/helpers/`nameof` change-dispatch checks retyped from `MainWindowViewModel` to the interface. **Step 2 below is now complete**: no dockable VM (or its view code-behind) references the concrete `MainWindowViewModel` type — each depends on a clean feature VM (`LogWindowViewModel`) or a narrow context interface (`ILayoutManagementContext`/`ILeftPanelContext`/`IRequestPanelContext`), all implemented by `MainWindowViewModel` as the composition root. Verified headlessly: `state-initial.png`/`request-tabs.png`/`variables-*.png` byte-identical, `state-response.png` renders the response body/status/actions correctly (differs only in non-deterministic elapsed-time text). New tests in `DockableViewModelDecouplingTests`.
>
> **Update 2026-06-14 (later 12):** Final cleanup — `DockFactory` no longer takes `MainWindowViewModel` at all. Its constructor now takes `ILayoutManagementContext`, `ILeftPanelContext`, and `IRequestPanelContext` directly (plus the existing `EnvironmentsViewModel`/`OptionsViewModel`/`CookieJarViewModel`/`LogWindowViewModel`), and `CreateLayout()` constructs `LeftPanelViewModel`/`LayoutManagementViewModel`/`RequestViewModel` from those typed fields, dropping `using Features.Main`. `MainWindowViewModel`'s constructor passes `this` three times (once per interface) at the `new DockFactory(...)` call site. Verified headlessly: `layout-panel.png`, `log-panel.png`, `collections-panel.png`, `collections-panel-tree.png`, `scheduled-jobs.png`, `state-initial.png`, `request-tabs.png`, and the `variables-*` screenshots are byte-identical before/after.

> **Update 2026-06-14 (later 13):** Started on the "move panel state out of Main into dedicated feature VMs" follow-up noted above, taking the smallest of the three remaining dockables (Layout). `LayoutManagementViewModel` (`Features/Layout/`) now owns the saved-layout collection (`SavedLayoutNames`/`SelectedLayoutName`) and the four layout commands (`SaveLayoutAsNewCommand`/`SaveLayoutToExistingCommand`/`RestoreDefaultLayoutCommand`/`RemoveLayoutCommand`) directly, including the `LayoutWorkflow`-backed `LoadFromOptions`/`BuildNamedLayouts` and the `WhenAnyValue(SelectedLayoutName)`-driven restore subscription — all moved verbatim from `MainWindowViewModel`. It takes only the five dock-tree/window-geometry delegates from Main (`refreshDockTreeCache`, `captureLayoutSnapshot`, `applyLayoutSnapshot`, `persistLayoutOptions`, `getDefaultLayout`), per the "Apply* UI projections stay in Main" precedent. `ILayoutManagementContext` is deleted; `MainWindowViewModel` no longer implements it. `DockFactory` now takes the `LayoutManagementViewModel` instance directly (same pattern as `LogPanelViewModel`/`LogWindowViewModel`) instead of constructing it from a context interface. `MainWindowViewModel` is down to about 2,527 lines. Tests: `DockableViewModelDecouplingTests.LayoutManagementViewModel_OwnsSavedLayoutStateAndCommands` replaces the old context-proxy test; `MainWindowLayoutUiTests` updated to go through `viewModel.LayoutManagement.*`. `dotnet test Arbor.HttpClient.slnx` reports 869/869 passing. The Explorer (`ILeftPanelContext`) and Request (`IRequestPanelContext`) slices remain as future, larger increments of this same follow-up.

> **Update 2026-06-15:** Implemented the registration-list `DockFactory` called out as optional/incremental future work. New `IDockPanelRegistration` interface (`Features/Layout/`) declares `Location` (`DockPanelLocation.LeftTool` or `.Document`) and `Dockable`. Each panel now has a small registration class in its own feature folder: `LeftPanelDockRegistration` (Collections), `OptionsDockRegistration` (Options), `EnvironmentsDockRegistration` (Environments), `LogPanelDockRegistration` (Logging), `CookieJarDockRegistration` (Cookies), `LayoutManagementDockRegistration` and `RequestDockRegistration` (HttpRequest). `DockFactory`'s constructor now takes `IReadOnlyList<IDockPanelRegistration>` instead of the seven individual feature-VM/context parameters; `CreateLayout()` groups registrations by `Location` to populate `left-tool-dock`/`request-dock` (first registration in each group becomes `ActiveDockable`), and the six typed dockable properties (`LeftPanelViewModel`, `OptionsViewModel`, etc.) are replaced by a single generic `GetDockable<T>()` lookup. `MainWindowViewModel` (the composition root) builds the registration list and passes it to `new DockFactory(...)`; its six `_dockFactory?.XxxViewModel` activation checks now call `_dockFactory?.GetDockable<XxxViewModel>()`. Adding a new dock panel now means adding one registration class plus one line in `MainWindowViewModel`'s registration list — `DockFactory` itself never changes. New tests: `DockFactoryRegistrationTests` (layout composition by location, `GetDockable<T>`, `UpdateLeftToolDock`, and a "new registration appears without DockFactory changes" pluggability test). `dotnet test Arbor.HttpClient.slnx` still passes (874/874).

- **Monolithic main view model**: `MainWindowViewModel` (~2,500 lines at the time of writing) owns request editing, response rendering, history, collections, environments, options, scheduling, layout persistence, and logging. All UI actions pass through this single type, so responsibilities are tightly coupled.
- **Child view models are thin proxies**: dockable VMs such as `RequestViewModel`, `ResponseViewModel`, `LeftPanelViewModel`, and `OptionsViewModel` simply forward to `MainWindowViewModel`. Reusing them elsewhere still drags the entire main VM with it.
- **Composition is hard-wired**: `App.axaml.cs` creates concrete services and VMs directly, and `DockFactory` constructs all dockables with a `MainWindowViewModel` dependency. Adding any new dock panel requires edits in both `DockFactory` and `MainWindowViewModel`, which blocks feature isolation.
- **Limited reuse boundaries**: UI pieces share `MainWindowViewModel` state instead of feature-specific contracts. Reusing the request panel or environment editor in another host window would require carrying the whole main VM.
- **Testability friction**: View models talk to concrete repositories, file system watchers, timers, and UI-only services directly. There are no focused unit tests for feature logic; existing tests are end-to-end.

## What is already working well

- The solution is split into clear top-level layers: `Core` (UI-agnostic logic), `Storage.Sqlite` (persistence), `Desktop` (UI).
- Core abstractions (`IRequestHistoryRepository`, `ICollectionRepository`, `IEnvironmentRepository`, `IScheduledJobRepository`) make storage replaceable and unit-testable.
- `HttpRequestService` and `VariableResolver` are well-isolated and already covered by unit tests.
- Shared test infrastructure (`Arbor.HttpClient.Testing`) provides in-memory repositories and fakes for use across all test projects.

## Answers to the issue questions

| Question | Answer |
|----------|--------|
| Are view models separated in a scalable way? | Partially. Initial separation exists but `MainWindowViewModel` is too central. |
| Can components be reused? | Core and repository abstractions are reusable. Desktop components all depend on `MainWindowViewModel`. |
| Can new features be added without touching existing files? | Not yet. Every feature currently requires edits to `MainWindowViewModel`, `DockFactory`, and `App.axaml.cs`. |
| Is the code easy to test? | Core is test-friendly. Feature logic embedded in `MainWindowViewModel` is not unit-testable in isolation. |

## Recommendations (incremental)

1. **Carve feature view models with clear contracts**
   - Introduce feature-specific VMs (e.g., `RequestEditorViewModel`, `EnvironmentPanelViewModel`, `OptionsPanelViewModel`, `SchedulerPanelViewModel`) that own their state and behavior.
   - Expose only the minimal interfaces each panel needs and have `MainWindowViewModel` compose them rather than implement everything itself.

2. **Modular dock registrations** ✅ Dockable decoupling complete (2026-06-14)
   - Let each feature provide a `DockableRegistration` (view, VM factory, initial placement) consumed by `DockFactory`. Adding a new tool/document should mean adding one registration class, not touching existing ones.
   - Done: no dockable VM (or its view code-behind) references the concrete `MainWindowViewModel` anymore. `LogPanelViewModel` depends on `LogWindowViewModel`; `LayoutManagementViewModel` on `ILayoutManagementContext`; `LeftPanelViewModel` on `ILeftPanelContext`; `RequestViewModel`/`EmbeddedResponseView` on `IRequestPanelContext`; and the shared `VariableTextBox` on `IVariableAutoCompleteHost`. `MainWindowViewModel` implements all the context interfaces as the composition root. `DockFactory` also receives `OptionsViewModel`/`EnvironmentsViewModel`/`CookieJarViewModel`/`LogWindowViewModel` directly.
   - Done (2026-06-14, later 12): `DockFactory` itself no longer depends on `MainWindowViewModel` either — its constructor takes `ILayoutManagementContext`, `ILeftPanelContext`, and `IRequestPanelContext` directly (alongside the existing feature VMs), and `CreateLayout()` builds `LeftPanelViewModel`/`LayoutManagementViewModel`/`RequestViewModel` from those typed fields. `MainWindowViewModel`'s constructor passes `this` for each interface via implicit conversion. A registration-list `DockFactory` remains optional/incremental future work.
   - Done (2026-06-14, later 13): `LayoutManagementViewModel` no longer depends on `ILayoutManagementContext` either — that interface is deleted, and the VM now owns the saved-layout state and the four layout commands directly, taking only the dock-tree/persistence delegates from Main. `DockFactory` takes the `LayoutManagementViewModel` instance directly (same pattern as `LogPanelViewModel`/`LogWindowViewModel`). `LeftPanelViewModel`/`ILeftPanelContext` and `RequestViewModel`/`IRequestPanelContext` remain as future increments of this same "move panel state out of Main" follow-up.
   - Done (2026-06-15): The registration-list `DockFactory` is implemented. `DockFactory` now takes `IReadOnlyList<IDockPanelRegistration>`; each panel has a one-file registration (`LeftPanelDockRegistration`, `OptionsDockRegistration`, `EnvironmentsDockRegistration`, `LogPanelDockRegistration`, `CookieJarDockRegistration`, `LayoutManagementDockRegistration`, `RequestDockRegistration`) declaring its `DockPanelLocation` and `Dockable`. `CreateLayout()` composes `left-tool-dock`/`request-dock` purely from these registrations, and `GetDockable<T>()` replaces the six previously-typed dockable properties. Adding a new dock panel now requires one new registration class plus one line in `MainWindowViewModel`'s registration list.

3. **Move infrastructure behind interfaces**
   - Wrap clipboard, storage provider, file system watcher, and timers behind interfaces injected into the relevant feature VMs. This enables headless unit testing and reduces Avalonia-specific coupling in business logic.

4. **Testing approach**
   - Add focused unit tests per feature VM using injected fakes from `Arbor.HttpClient.Testing`. Reserve E2E tests for integration confidence.
   - Start with the request editor (URL/headers/body/variables) and scheduler once their dependencies are interface-based.
   - **[REQUIRED]** Test project boundaries must mirror library boundaries. Tests for a library project (e.g. `Arbor.HttpClient.Storage.Sqlite`) must live in a test project that references **only** that library plus `Arbor.HttpClient.Testing`. Do not add cross-layer `<ProjectReference>` entries to an existing test project. If no dedicated test project exists for the library being tested, create one (e.g. `Arbor.HttpClient.Storage.Sqlite.Tests`).

5. **Reuse guidelines**
   - Extract reusable UI controls (request headers list, query parameters list, response body viewer) so they consume feature interfaces instead of the whole main VM.
   - Keep shared models (`RequestHistoryEntry`, `ResolvedHttpRequestDraft`, `RequestEnvironment`, etc.) in `Arbor.HttpClient.Core` so they stay UI-agnostic.

## Folder structure (feature-centric)

All code is now organized into feature-centric vertical slices rather than type-based horizontal layers. The old `Models/`, `Services/`, `Abstractions/`, `ViewModels/`, `Views/`, `Converters/` directories have been removed.

### `Arbor.HttpClient.Core`

```
Collections/     — ICollectionRepository, Collection, CollectionRequest
Environments/    — IEnvironmentRepository, EnvironmentVariable, RequestEnvironment
GraphQl/         — GraphQlService, GraphQlDraft
HttpRequest/     — IRequestHistoryRepository, RequestHistoryEntry, ResolvedHttpRequestDraft, HttpResponseDetails,
                   HttpRequestDiagnostics, RequestHeader, RequestType, HttpRequestService, CurlFormatter
OpenApiImport/   — OpenApiImportService
ScheduledJobs/   — IScheduledJobRepository, ScheduledJobConfig
Scripting/       — IScriptRunner, ScriptContext, ScriptResponse, ScriptResult
Sse/             — SseService, SseEvent
Variables/       — VariableResolver
WebSocket/       — WebSocketService, WebSocketMessage
```

### `Arbor.HttpClient.Desktop`

```
Demo/                     — DemoServer
Features/
  About/                  — AboutWindowViewModel, AboutWindow
  Collections/            — CollectionsWorkflow, CollectionsManagementCoordinator, CollectionUrlHelper,
                            CollectionInheritedHeadersWorkflow, CollectionFilterWorkflow,
                            CollectionRequestEditorProjectionWorkflow,
                            CollectionGroupViewModel, CollectionItemViewModel, LeftPanelViewModel,
                            ILeftPanelContext, LeftPanelView, BoolToExpandIconConverter
  Cookies/                — CookieJarViewModel, CookieEntryViewModel, CookieJarView
  Demo/                   — DemoDataWorkflow, DemoServerLifecycleCoordinator
  Diagnostics/            — DiagnosticsViewModel, DiagnosticsOptions, DiagnosticsWindow,
                            UnhandledExceptionCollector, UnhandledExceptionEntry
  Environments/           — EnvironmentsViewModel, EnvironmentVariableViewModel, EnvironmentsView
  GraphQl/                — GraphQlViewModel, GraphQlRequestWorkflow, ManualGraphQlRequestCoordinator
  History/                — RequestHistoryWorkflow
  HttpRequest/            — RequestEditorViewModel, RequestViewModel, IRequestPanelContext, RequestTabViewModel, RequestTabsWorkflow, ResponseViewModel,
                            HttpRequestWorkflow, ManualHttpRequestCoordinator, HttpResponseProjectionWorkflow,
                            ResponseActionsViewModel, IResponseActionsContext, RequestBodyExternalEditWorkflow,
                            RequestHeaderViewModel, RequestQueryParameterViewModel,
                            RequestView, ResponseView, EmbeddedResponseView,
                            MethodToColorConverter, StatusCodeToColorConverter
  Layout/                 — DockFactory, DockLayoutSnapshot, DockTreeNode, FloatingWindowSnapshot,
                            LayoutWorkflow, LayoutTreeWorkflow, LayoutManagementViewModel,
                            LayoutManagementView, LayoutOptions, NamedDockLayout,
                            DraftPersistenceService, DraftHeaderDto, RequestEditorSnapshot
  Logging/                — InMemorySink, LogEntry, LogTab, LogPanelViewModel,
                            LogWindowViewModel, LogPanelView, LogWindow
  Main/                   — MainWindowViewModel, MainWindow, ResponseSaveFileNamePatternFormatter
  Options/                — ApplicationOptions, AppearanceOptions, HttpOptions, ApplicationOptionsStore,
                            ApplicationOptionsWorkflow, ApplicationOptionsSnapshot, OptionsPersistenceOutcome,
                            OptionsViewModel, OptionsView, per-page option views (HttpOptionsPageView,
                            LookAndFeelOptionsPageView, DiagnosticsOptionsPageView, ManageOptionsPageView,
                            ScheduledJobsOptionsPageView)
  ScheduledJobs/          — ScheduledJobService, ScheduledJobsWorkflow, ScheduledJobViewModel, ScheduledJobsOptions
  Scripting/              — RoslynScriptRunner, ScriptViewModel
  Sse/                    — SseViewModel
  Streaming/              — StreamingConnectionWorkflow
  Variables/              — VariableAutoCompleteController, VariableCompletionData, VariableCompletionEngine,
                            VariableNameHelper, VariableTextBox, IVariableAutoCompleteHost, VariableTokenColorizer
  WebSocket/              — WebSocketViewModel
  WebView/                — WebViewWindow
Localization/             — Strings.resx, Strings.Designer.cs
Shared/                   — ReactiveViewModelBase, ReactiveToolBase, DisposableExtensions, HttpContentTypeHelper,
                            TextHelpers, NotNullConverter, StringEqualityConverter, TabFontWeightConverter
```

## Additional questions to track continuously

- Does every new feature have at least one test that does not require the full UI runtime?
- Can a feature be disabled or replaced without modifying unrelated features?
- Are cross-feature dependencies explicit (constructor interfaces) rather than implicit (shared mutable state)?
- How should state persistence be scoped — per feature vs global auto-save? What is the rollback story if persistence fails?
- How do we handle threading and cancellation consistently across features (request sending, file watching, scheduled jobs)?
- What accessibility guarantees must each new panel meet (keyboard navigation, contrast, screen-reader labels)?

## Ordered next steps

0. Use the execution plan in [`docs/architecture/mainwindowviewmodel-split-plan.md`](mainwindowviewmodel-split-plan.md) for incremental extraction work and communication-pattern decisions — see its "Remaining slices — ordered plan" section for the current slice order (next up: Request tabs + history).
   - **2026-06-15:** A new holistic target — moving the remaining Explorer/Request panel state out of `MainWindowViewModel` into feature VMs and replacing the `ILeftPanelContext`/`IRequestPanelContext` façades with a UI-agnostic Rx message bus — is captured in [`mainwindowviewmodel-redesign-plan.md`](mainwindowviewmodel-redesign-plan.md). It is delivered incrementally (one slice per PR).
1. ~~Feature-centric folder structure~~ ✅ Complete — all projects now use vertical-slice feature folders.
2. ~~Refactor `DockFactory` to consume feature registrations and stop requiring a `MainWindowViewModel` reference.~~ ✅ Complete (2026-06-15) — `DockFactory` takes `IReadOnlyList<IDockPanelRegistration>`; each panel's registration lives in its own feature folder.
3. ~~Evaluate whether a mediator/event-bus is needed after 1–2 successful slice extractions.~~ ✅ Resolved — several extractions later, direct Workflow/Coordinator calls plus `IObservable<T>` endpoints cover all communication needs; mediator/event-bus rejected (see the split plan's "Current guidance" section).
4. Introduce a DI container (e.g., `Microsoft.Extensions.DependencyInjection`) only when constructor/composition friction remains significant after slice extraction.
5. ~~Consider RX.NET (`System.Reactive`) as an additive communication channel (Option E)~~ ✅ Superseded — the solution completed a full ReactiveUI/Rx.NET migration (PRs #218–#220); see [`reactiveui-migration-progress.md`](reactiveui-migration-progress.md). [`rx-reactive-evaluation.md`](rx-reactive-evaluation.md) is kept for historical context.
