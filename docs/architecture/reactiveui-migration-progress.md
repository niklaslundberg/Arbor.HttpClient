# ReactiveUI Migration — Session Progress

> Working state of the full ReactiveUI/Rx.NET migration. Companion to
> [`reactiveui-migration-plan.md`](reactiveui-migration-plan.md) and
> [`rx-reactive-evaluation.md`](rx-reactive-evaluation.md).
> **Decision taken (overrides the evaluation's earlier recommendation): Full ReactiveUI migration** —
> the owner explicitly requested migrating the whole solution to ReactiveUI + Rx.NET, preferring
> functional style.

**Branch:** `reactiveui-migration` (based on `main` @ `2f9bea7`)
**Status as of 2026-06-11:** Phases 1–5 complete and committed; all 675 tests passed as of the
last verified run (end of Phase 5). Phase 6 (final cleanup) — `ViewModelBase`/`CommunityToolkit.Mvvm`
removal and doc updates are done; **build/test/coverage verification is still outstanding** because
this session's sandbox has no `dotnet` SDK (see Phase 6 item 4 below).

## Commits on the branch

| Commit | Phase |
|---|---|
| `b6e49d9` | 1 — Foundation: packages, `UseReactiveUI`, `ReactiveViewModelBase` |
| `ff0094d` | 2 — Leaf ViewModels |
| `c14e76b` | 3 — Mid-tier feature ViewModels (incl. RequestEditorViewModel) |
| `aacb0ab` | 4 — Dock.Model.Mvvm → Dock.Model.ReactiveUI |
| `7e4a7cd` | 5 — MainWindowViewModel |

## What was done

### Phase 1 — Foundation (`b6e49d9`)
- Packages added to `Directory.Packages.props` (all MIT, recorded in `THIRD_PARTY_NOTICES.md`):
  `ReactiveUI` 23.2.28, `ReactiveUI.Avalonia` 12.0.3, `ReactiveUI.SourceGenerators` 3.1.0
  (compile-time only, `PrivateAssets=all`), `Microsoft.Reactive.Testing` 6.1.0 (declared; not yet
  consumed by a test project).
- **Avalonia bumped 12.0.2 → 12.0.4** (required by ReactiveUI.Avalonia 12.0.3). Lock files regenerated.
- `Program.BuildAvaloniaApp()` now calls `.UseReactiveUI(builder => builder.WithAvalonia())`
  (ReactiveUI 23 uses a mandatory builder; namespace is `ReactiveUI.Avalonia`, **not** `Avalonia.ReactiveUI`).
- New `Shared/ReactiveViewModelBase.cs`: `ReactiveObject, IDisposable` with a protected
  `CompositeDisposable Disposables`, a `Dispose(bool)` template method, and a
  `PropertyChangedObservable` bridge (same member the old `ViewModelBase` exposed).
- New `Shared/DisposableExtensions.cs` with `DisposeWith` — **ReactiveUI 23 removed
  `DisposableMixins.DisposeWith`**, so the project provides its own.

### Phase 2 — Leaf ViewModels (`ff0094d`)
Converted to `ReactiveViewModelBase` + `[Reactive]` / `[ReactiveCommand]` source generators:
CookieEntry, RequestHeader, RequestQueryParameter, EnvironmentVariable, AboutWindow, Diagnostics
(+ UnhandledExceptionEntry), CollectionGroup, LogWindow, Script view models.
- CTK `partial void OnXChanged` hooks → constructor `WhenAnyValue(...).Skip(1).Subscribe(...)`.
- Derived properties (`IsValueMasked`, `IsExpired`) → `ObservableAsPropertyHelper` (OAPH).
- E2E test assembly: `ReactiveUiTestInitializer` module initializer
  (ReactiveUI 23 throws until initialized); `TestAppBuilder` also calls `UseReactiveUI`.

### Phase 3 — Mid-tier ViewModels (`c14e76b`)
WebSocket, Sse, GraphQl, ScheduledJob, RequestTab, RequestEditor view models.
- `ScheduledJobViewModel`: 8 auto-save hooks merged into one `WhenAnyValue` pipeline (two groups of 4 —
  **`WhenAnyValue` tuple overloads top out below 8 expressions**); `IsWebViewApplicable`,
  `IsWebViewEnabled`, `HasLastResponse` are OAPHs.
- `RequestEditorViewModel`: 15 computed properties → OAPHs; string-matching
  `PropertyChangedObservable` pipelines → typed `WhenAnyValue` subscriptions with `Where` gates;
  `Apply*` dispatcher methods deleted.
- `RequestTabViewModel.DisplayTitle` derived from `RequestEditor.WhenAnyValue(x => x.RequestName)`.
- `StreamingConnectionWorkflow` + tests moved to the ReactiveCommand `Execute()` API.

### Phase 4 — Dock switch (`aacb0ab`)
- `Dock.Model.Mvvm` → `Dock.Model.ReactiveUI` 12.0.0.2 (namespace swap across DockFactory,
  ViewLocator, LayoutTreeWorkflow, all Tool/Document VMs).
- CookieJar, Environments, Options VMs converted to `[Reactive]`/`[ReactiveCommand]`/OAPH.
- Cross-VM command proxies (LeftPanel, LayoutManagement, MainWindow environment proxies) retyped to
  `System.Windows.Input.ICommand` so they bind regardless of the underlying command framework.

### Phase 5 — MainWindowViewModel (`7e4a7cd`)
- 55 `[ObservableProperty]` → `[Reactive]`, 53 `[RelayCommand]` → `[ReactiveCommand]`,
  `OnPropertyChanged(` → `this.RaisePropertyChanged(`.
- `SendRequestCommand`/`LoadHistoryCommand` are `ReactiveCommand<Unit, Unit>`.
  **Cancellation design:** a linked `CancellationTokenSource` field (`_sendRequestCts`) cancelled by
  `ExecutePrimaryAction` — deliberately *not* `TakeUntil`, so `IsExecuting` stays true until
  `SendRequestAsync` finishes its cancellation handling (preserves old
  `AsyncRelayCommand.Cancel()`/`ExecutionTask` semantics, e.g. `ErrorMessage = "Request cancelled."`).
- `IsRequestInProgress` = OAPH over `IsExecuting`; `ThrownExceptions` of both commands logged.
- `Dispose()` → `protected override Dispose(bool)`.
- Tests: `await cmd.Execute()` replaces `ExecuteAsync(null)`; sync invocations use
  `cmd.Execute(...).Subscribe()` (a bare `Execute(param)` is a **cold observable — silent no-op**);
  cancellation/timeout tests await `IsExecuting.SkipWhile(!e).Where(!e).FirstAsync()`.

## Hard-won gotchas (do not rediscover)

1. **ReactiveUI 23 must be initialized** (`RxAppBuilder`) before any `WhenAnyValue`; the app does it
   via `UseReactiveUI`, tests via module initializer. Double init is idempotent **but
   first-registered services win** — the test initializer must use `.WithAvalonia()` (not just
   `.WithCoreServices()`), otherwise command notifications fire on worker threads and crash bound
   `MenuItem`s mid-test-run.
2. `ReactiveCommand.Execute(param)` returns a **cold** observable; nothing happens without
   `Subscribe()`/`await`. Typed-parameter calls compile silently — behavioral no-op bugs.
3. ReactiveUI.SourceGenerators provides **no `OnXChanged` partial hooks** — use ctor `WhenAnyValue`
   subscriptions; it **does** strip the `Async` suffix for command names like CTK.
4. `DisposeWith` no longer ships in ReactiveUI core (see `Shared/DisposableExtensions.cs`).
5. Awaiting `IObservable<T>` / `ToTask()` need `System.Reactive.Linq` /
   `System.Reactive.Threading.Tasks` usings in test files.

## Phase 6 — final cleanup (this session)

1. ✅ **Deleted `src/Arbor.HttpClient.Desktop/Shared/ViewModelBase.cs`** (last CTK usage in
   production code). Renamed `ViewModelBaseRxTests` → `ReactiveViewModelBaseRxTests` and retargeted
   `TestViewModel` to `ReactiveViewModelBase` (`SetProperty` → `this.RaiseAndSetIfChanged`, requires
   `using ReactiveUI;`). Removed `ViewModelBase` from `ViewLocator.Match`.
2. ✅ **Removed `CommunityToolkit.Mvvm`** from `Arbor.HttpClient.Desktop.csproj`,
   `Directory.Packages.props`, and `THIRD_PARTY_NOTICES.md`.
   ⚠️ **`dotnet` CLI is unavailable in this sandbox** — `packages.lock.json` for
   `Arbor.HttpClient.Desktop` and `Arbor.HttpClient.Desktop.E2E.Tests` were edited by hand to drop
   the `CommunityToolkit.Mvvm` entries (Direct in Desktop, CentralTransitive in E2E.Tests, plus the
   transitive-dependency list under the `arbor.httpclient.desktop` project node). **Before merging,
   run `dotnet restore Arbor.HttpClient.slnx --force-evaluate` on a machine with the .NET SDK and
   commit any further lock-file diff** (it should be a no-op if the manual edit was correct).
3. ✅ **Docs per repo process:**
   - `docs/suggestions/ReactiveUIAdoptionScope.md`: decision "Full ReactiveUI migration" recorded at
     the top, with a checklist and links from `rx-reactive-evaluation.md` and
     `reactiveui-migration-plan.md` (acceptance criteria met).
   - `reactiveui-migration-plan.md` §4 checklist ticked off; deviations noted (TestScheduler not yet
     consumed by tests — deferred to `SchedulerInjection.md`; DynamicData not yet adopted — see
     `DynamicDataCollections.md`; ReactiveUI 23 mandatory-builder API).
   - `docs/architecture/clean-feature-separation.md` `Shared/` listing updated
     (`ViewModelBase` → `ReactiveViewModelBase`, `ReactiveToolBase`).
   - `docs/ux-ideas.md` reviewed — no UI/UX changed in this session (pure VM/dependency cleanup),
     so no items moved or added.
4. ⚠️ **Outstanding — requires a machine with the .NET SDK (not available in this sandbox):**
   - `dotnet restore Arbor.HttpClient.slnx --force-evaluate` (verify lock-file edits above).
   - `dotnet build Arbor.HttpClient.slnx --configuration Release` — confirm clean-build warning
     count stays ≤ the main baseline (91 warnings).
   - `dotnet test Arbor.HttpClient.slnx --configuration Release` — confirm all tests still pass
     (675 at HEAD before this change) and that the renamed/retargeted
     `ReactiveViewModelBaseRxTests` passes.
   - `dotnet list Arbor.HttpClient.slnx package --vulnerable --include-transitive`.
   - `dotnet test ... --collect:"XPlat Code Coverage"` and update `docs/coverage.md` if the
     Desktop coverage percentage moved.
   - Runtime validation per copilot-instructions §6 (headless run, no binding errors). No XAML
     changed in this session, so screenshots are not required.
5. **Optional follow-ups from the plan (not started, separate PRs):**
   - `ScheduledJobService`: `PeriodicTimer` → `Observable.Interval(IScheduler)` + `TestScheduler`
     tests (`docs/suggestions/SchedulerInjection.md`) — `Microsoft.Reactive.Testing` is already pinned.
   - DynamicData for the Collections panel filter/sort (`docs/suggestions/DynamicDataCollections.md`).
   - Replace remaining `PropertyChangedObservable` bridge usages with `WhenAnyValue`/`.Changed`
     and then drop the bridge from `ReactiveViewModelBase`.
   - Interactions (`Interaction<TIn,TOut>`) for dialogs per plan §3.3/§3.5.

## Verification commands

```powershell
dotnet build Arbor.HttpClient.slnx --configuration Release
dotnet test Arbor.HttpClient.slnx --configuration Release   # 675 tests, all green at HEAD
```
