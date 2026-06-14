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

2. **Modular dock registrations**
   - Let each feature provide a `DockableRegistration` (view, VM factory, initial placement) consumed by `DockFactory`. Adding a new tool/document should mean adding one registration class, not touching existing ones.

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
                            LeftPanelView, BoolToExpandIconConverter
  Cookies/                — CookieJarViewModel, CookieEntryViewModel, CookieJarView
  Demo/                   — DemoDataWorkflow, DemoServerLifecycleCoordinator
  Diagnostics/            — DiagnosticsViewModel, DiagnosticsOptions, DiagnosticsWindow,
                            UnhandledExceptionCollector, UnhandledExceptionEntry
  Environments/           — EnvironmentsViewModel, EnvironmentVariableViewModel, EnvironmentsView
  GraphQl/                — GraphQlViewModel, GraphQlRequestWorkflow, ManualGraphQlRequestCoordinator
  History/                — RequestHistoryWorkflow
  HttpRequest/            — RequestEditorViewModel, RequestViewModel, RequestTabViewModel, RequestTabsWorkflow, ResponseViewModel,
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
                            VariableNameHelper, VariableTextBox, VariableTokenColorizer
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
1. ~~Feature-centric folder structure~~ ✅ Complete — all projects now use vertical-slice feature folders.
2. Refactor `DockFactory` to consume feature registrations and stop requiring a `MainWindowViewModel` reference. **Decision 2026-06-11: deliberately deferred until after the remaining `MainWindowViewModel` slices are extracted**, so registrations can target clean feature VMs.
3. ~~Evaluate whether a mediator/event-bus is needed after 1–2 successful slice extractions.~~ ✅ Resolved — several extractions later, direct Workflow/Coordinator calls plus `IObservable<T>` endpoints cover all communication needs; mediator/event-bus rejected (see the split plan's "Current guidance" section).
4. Introduce a DI container (e.g., `Microsoft.Extensions.DependencyInjection`) only when constructor/composition friction remains significant after slice extraction.
5. ~~Consider RX.NET (`System.Reactive`) as an additive communication channel (Option E)~~ ✅ Superseded — the solution completed a full ReactiveUI/Rx.NET migration (PRs #218–#220); see [`reactiveui-migration-progress.md`](reactiveui-migration-progress.md). [`rx-reactive-evaluation.md`](rx-reactive-evaluation.md) is kept for historical context.
