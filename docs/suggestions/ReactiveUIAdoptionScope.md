# Task: Determine the adoption scope for ReactiveUI

## Decision: Full ReactiveUI migration

The project has migrated the entire `Arbor.HttpClient.Desktop` ViewModel layer to
**ReactiveUI** (`ReactiveObject`, `ReactiveCommand`, `ObservableAsPropertyHelper`,
`WhenAnyValue`) on top of Rx.NET, replacing `CommunityToolkit.Mvvm`. This overrides the
"additive only" recommendation in
[`rx-reactive-evaluation.md`](../architecture/rx-reactive-evaluation.md) — the owner
explicitly requested a full migration, preferring the functional reactive style.

Migration tasks (see [`reactiveui-migration-plan.md`](../architecture/reactiveui-migration-plan.md)
§4 for the checklist and [`reactiveui-migration-progress.md`](../architecture/reactiveui-migration-progress.md)
for the session-by-session record):

- [x] Add ReactiveUI/Rx.NET packages and `THIRD_PARTY_NOTICES.md` entries
- [x] Call `.UseReactiveUI()` in `Program.cs`
- [x] Introduce `ReactiveViewModelBase` / `ReactiveToolBase` and replace `ViewModelBase` inheritance everywhere
- [x] Convert all `[ObservableProperty]`/`[RelayCommand]` view models to `[Reactive]`/`[ReactiveCommand]` (ReactiveUI.SourceGenerators)
- [x] Switch `Dock.Model.Mvvm` → `Dock.Model.ReactiveUI`
- [x] Delete `Shared/ViewModelBase.cs` and remove `CommunityToolkit.Mvvm` from the solution entirely
- [ ] DynamicData for the Collections panel (tracked separately, see `DynamicDataCollections.md`)
- [ ] `ScheduledJobService` timer → `Observable.Interval` + `IScheduler` (tracked separately, see `SchedulerInjection.md`)
- [ ] `Interaction<TIn,TOut>` for dialogs (plan §3.3/§3.5, not started)

> **Note on the sections below:** the "Description", "Acceptance Criteria", and "Tests to Create"
> sections were written when this was an open *decision* task, before any code changed. The
> migration described above is now implemented, so those sections are kept as a historical record
> of the original task framing rather than outstanding work — the live status lives in the
> "Decision" section above and in `reactiveui-migration-progress.md`.

**Description**
- Decide whether the project will adopt only `System.Reactive` (lightweight observable streams) while keeping `CommunityToolkit.Mvvm` for property generation, or perform a full migration to `ReactiveUI` as the ViewModel base.
- Document the decision, the expected migration steps, and any constraints.

**Acceptance Criteria**
1. The decision ("System.Reactive only" or "Full ReactiveUI migration") is clearly stated at the top of this file.
2. If "System.Reactive only" is chosen, a brief outline of how the existing MVVM toolkit will continue to be used alongside Rx streams is provided.
3. If "Full ReactiveUI migration" is chosen, a checklist of migration tasks (replace `ViewModelBase` with `ReactiveViewModelBase`, convert all commands to `ReactiveCommand`, etc.) is listed.
4. The document is linked from `docs/architecture/rx-reactive-evaluation.md` and `docs/architecture/reactiveui-migration-plan.md`.
5. No code changes are made yet.

**Tests to Create**
- No tests are required for the decision document itself, but a CI sanity check can verify that this file exists and contains one of the two allowed decision strings (e.g., a small script that greps for `System.Reactive only` or `Full ReactiveUI migration`).