# ReactiveUI Migration Plan for Arbor.HttpClient

> **Historical document.** The **Goal** below describes the *original, additive* proposal — keep
> `CommunityToolkit.Mvvm` and layer ReactiveUI on top selectively. That proposal was
> **superseded**: see [`docs/suggestions/ReactiveUIAdoptionScope.md`](../suggestions/ReactiveUIAdoptionScope.md)
> for the **full ReactiveUI migration** decision that was implemented instead.
> `CommunityToolkit.Mvvm` has since been removed entirely from the solution. The
> feature-by-feature steps and §4 checklist below remain useful as a record of what was actually
> done; see [`reactiveui-migration-progress.md`](reactiveui-migration-progress.md) for the
> session-by-session record and deviations from this plan.

**Goal (original, additive proposal — superseded, see above)** – Introduce **ReactiveUI** (and the underlying **Rx.NET** streams) into the Avalonia UI layer while preserving the existing incremental feature‑slice architecture. The plan is additive: we keep `CommunityToolkit.Mvvm` for property generation where it already works and layer ReactiveUI on top for cross‑feature communication, async commands, and observable collections.

---

## 1️⃣ Prerequisites & Boilerplate

| Step | Action | Rationale |
|------|--------|-----------|
| 1️⃣ Add NuGet packages | `Avalonia.ReactiveUI`, `System.Reactive`, optionally `DynamicData` | Provides `UseReactiveUI()`, `ReactiveObject`, `ReactiveCommand`, and observable change‑sets. |
| 2️⃣ Update `Directory.Packages.props` | Add entries for the packages with **Apache‑2.0** license (compatible with the MIT project). Add a matching entry to `THIRD_PARTY_NOTICES.md`. | Required by the repository policy (see **License Compatibility** section). |
| 3️⃣ Enable ReactiveUI in the app builder | In `src/Arbor.HttpClient.Desktop/Program.cs` call `.UseReactiveUI()` on the `AppBuilder`. | Registers schedulers and view‑discovery.
| 4️⃣ Add `ReactiveObject` base | Create a new abstract class `ReactiveViewModelBase : ReactiveObject, IDisposable` in `Shared/ViewModelBase.cs` (replace the existing `ViewModelBase` inheritance). The class should expose a protected `CompositeDisposable _disposables = new();` and implement `Dispose()` that disposes it. | Gives every view‑model a unified reactive base and deterministic cleanup. |

> **Note** – Existing `ViewModelBase` already provides `RaiseAndSetIfChanged`. The new `ReactiveViewModelBase` will replace it; after migration each VM should inherit from this class.

---

## 2️⃣ Cross‑Feature Communication Pattern (Option E – Rx.NET observable streams)

We will introduce a thin **event‑bus** per feature rather than a global bus. Each feature VM exposes an `IObservable<T>` (or `Interaction<TIn,TOut>`) and a `Subject<T>` for publishing.

```csharp
public interface IRequestSelection
{
    IObservable<RequestEditorViewModel?> SelectedRequestChanged { get; }
    void SetSelectedRequest(RequestEditorViewModel? vm);
}
```

Implementation lives in the feature VM and is injected where needed (e.g., into `EnvironmentsViewModel`). This satisfies the **decoupling** goal from `clean-feature-separation.md` while allowing throttling, combining, and testable scheduling.

---

## 3️⃣ Feature‑by‑Feature Migration Steps

> The order follows the current slice‑extraction plan (`mainwindowviewmodel-split-plan.md`). Each step should be **independent** and have its own unit tests before moving to the next feature.

### 3.1 Main Window (`MainWindowViewModel`)
1. Replace the existing base class with `ReactiveViewModelBase`.
2. Convert the `SendRequest` async logic to a `ReactiveCommand<Unit, HttpResponseMessage>`.
   ```csharp
   SendRequest = ReactiveCommand.CreateFromTask(
       async ct => await _requestService.SendAsync(Draft, ct),
       this.WhenAnyValue(x => x.IsBusy).Select(b => !b));
   SendRequest.IsExecuting.BindTo(this, x => x.IsBusy);
   ```
3. Bind `SendRequest.ThrownExceptions` to the global logger.
4. Update `MainWindow.axaml` bindings: `Command="{Binding SendRequest}"` (no need for `CommandParameter`).
5. Add a unit test that asserts `SendRequest.CanExecute` toggles based on `IsBusy` using `TestScheduler`.

### 3.2 Request Editing (`RequestEditorViewModel`)
1. Inherit from `ReactiveViewModelBase`.
2. Replace manual `INotifyPropertyChanged` properties (e.g., `Url`, `Method`) with `[ObservableProperty]` **or** `this.RaiseAndSetIfChanged` – keep the source generator for boilerplate but expose an **observable** stream:
   ```csharp
   public IObservable<string> UrlChanged => this.WhenAnyValue(x => x.Url);
   ```
3. Use a `Subject<string>` for the **search‑box** in the variable auto‑complete engine and apply `Throttle(TimeSpan.FromMilliseconds(300))`.
4. Update the corresponding XAML to bind via `^` syntax (Avalonia’s observable binding):
   ```xml
   <TextBox Text="{Binding Url, Mode=TwoWay}" />
   ```
   No changes needed; ReactiveUI simply observes the property.
5. Add a test that verifies the throttled search emits after the debounce period.

### 3.3 Response View (`ResponseViewModel`)
1. Inherit from `ReactiveViewModelBase`.
2. Replace any manual `IsLoading` flag with a derived `ObservableAsPropertyHelper<bool>` bound to the `SendRequest.IsExecuting` observable from the parent VM.
   ```csharp
   _isLoading = parent.SendRequest.IsExecuting
       .ToProperty(this, x => x.IsLoading);
   ```
3. Add an `Interaction<string, bool>` called `ShowSaveDialog` to request a file‑save location from the UI.
4. Unit‑test that the interaction is invoked when the command executes.

### 3.4 Collections Panel (`CollectionsWorkflowViewModel`)
1. Introduce **DynamicData** to manage the observable collection of `CollectionGroupViewModel`.
   ```csharp
   private readonly SourceCache<CollectionGroupViewModel, string> _source = new(g => g.Id);
   public ReadOnlyObservableCollection<CollectionGroupViewModel> FilteredGroups { get; }
   _source.Connect()
          .Filter(this.WhenAnyValue(x => x.FilterText).Select(BuildFilter))
          .Sort(SortExpressionComparer<CollectionGroupViewModel>.Ascending(g => g.Name))
          .ObserveOn(RxApp.MainThreadScheduler)
          .Bind(out var list)
          .Subscribe();
   FilteredGroups = list;
   ```
2. Replace the old `ObservableCollection<...>` with the read‑only collection above.
3. Add unit tests using `TestScheduler` to verify filtering and sorting work deterministically.

### 3.5 Environments Panel (`EnvironmentsViewModel`)
1. Expose an `IObservable<RequestEnvironment?> SelectedEnvironmentChanged` via `WhenAnyValue`.
2. Subscribe to the `SelectedRequestChanged` observable from the request editor to update the environment when a request changes.
3. Use `Interaction<EnvironmentVariable, bool>` for confirming deletions.

### 3.6 Scheduled Jobs (`ScheduledJobService` & `ScheduledJobsOptions`)
1. Replace the `PeriodicTimer` implementation with `Observable.Interval(TimeSpan, IScheduler)`.
2. Inject an `IScheduler` (default: `TaskPoolScheduler.Default`). In tests inject `TestScheduler` and advance virtual time.
3. Expose a `IObservable<JobResult>` for UI panels to subscribe to job completions.
4. Add a test that executes a job after a virtual interval without real delays.

### 3.7 Other Feature Panels (Cookies, GraphQL, SSE, WebSocket, etc.)
* Follow the same pattern: inherit from `ReactiveViewModelBase`, expose needed observables, replace `AsyncRelayCommand` with `ReactiveCommand`, and use `Interaction<TIn,TOut>` for dialogs.
* Update XAML bindings where the command type changed (no longer need `CanExecute` converters).

---

## 4️⃣ Migration Checklist per Feature

| ✅ Done | Item |
|--------|------|
| ✅ | Add ReactiveUI package references and license entry |
| ✅ | Call `.UseReactiveUI()` in `Program.cs` |
| ✅ | Introduce `ReactiveViewModelBase` and replace inheritance (all VMs; `ViewModelBase` deleted) |
| ✅ | Convert at least one **AsyncRelayCommand** to **ReactiveCommand** (all commands converted, solution-wide) |
| ✅ | Add an observable stream for a shared state (e.g., `RequestTabViewModel.DisplayTitle` derived via `RequestEditor.WhenAnyValue(x => x.RequestName)`) |
| ☐ | Write unit tests covering the new reactive flow with `TestScheduler` — `Microsoft.Reactive.Testing` is referenced but not yet consumed by a test project; deferred to the `SchedulerInjection` follow-up |
| ☐ | Verify UI still builds and all existing integration tests pass (`dotnet test`) — pending; see "Outstanding" item 4 in `reactiveui-migration-progress.md` (no .NET SDK in the Phase 6 session) |
| ✅ | Update screenshots if UI changes (see `docs/screenshots/` workflow) — no XAML changes in this migration, so none required |

---

## 5️⃣ Documentation & Tracking

* Add this file to the repo: `docs/architecture/reactiveui-migration-plan.md`.
* Update `docs/architecture/clean-feature-separation.md` to reference the new ReactiveUI option (E) in the **communication‑pattern matrix**.
* Add a bullet in `docs/architecture/clean-feature-separation.md` under *Recommendations* → *Introduce System.Reactive* linking to this plan.
* When the migration is complete for a feature, add a checklist entry in the **PR Compliance Checklist** (section 16 of `.github/copilot-instructions.md`).

---

## 6️⃣ Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Increased bundle size (~2 MB) | Slightly larger installer | Acceptable for desktop app; monitor size in CI (`dotnet publish -c Release`). |
| Learning curve for developers | Slower onboarding | Provide a short internal doc with common Rx patterns (throttle, combine, Interaction) and add code‑search examples (use `code_search` skill). |
| Breaking existing tests due to command type change | CI failures | Migrate tests alongside code; keep a compatibility shim (`new ReactiveCommand(...).Execute()` works like the old `AsyncRelayCommand`). |
| Mixing `CommunityToolkit.Mvvm` and `ReactiveUI` may cause duplicate `INotifyPropertyChanged` implementations | Runtime warnings | Keep source‑generated properties for simple POCOs; use ReactiveUI only for streams and commands. |

---

## 7️⃣ Timeline (approx.)

| Week | Milestone |
|------|-----------|
| 1 | Add packages, `ReactiveViewModelBase`, and migrate `MainWindowViewModel` (command, logger). |
| 2 | Migrate `RequestEditorViewModel` + add throttled search. |
| 3 | Implement DynamicData collection for **Collections** panel. |
| 4 | Introduce observable environment selection and interactions. |
| 5 | Replace `ScheduledJobService` timer with `Observable.Interval`. |
| 6 | Sweep remaining feature panels (Cookies, GraphQL, SSE, WebSocket). |
| 7 | Full regression test run, update screenshots, final documentation. |

---

## 8️⃣ Next Actions (Immediate)
1. **Create a PR** that adds the NuGet references and updates `Directory.Packages.props`.
2. **Add entry to `THIRD_PARTY_NOTICES.md`** for `System.Reactive` and `Avalonia.ReactiveUI`.
3. **Implement `ReactiveViewModelBase`** and run the full test suite to ensure no breakage.
4. **Open a ticket** to track the migration of each feature (e.g., `#rxui‑main‑window`, `#rxui‑request‑editor`).

---

*This plan is aligned with the repository’s hard stops (all tests must pass, no secret leakage) and the architecture recommendations in `clean-feature-separation.md`. It provides a clear, incremental path to adopt ReactiveUI while keeping the existing slice‑based structure intact.*
