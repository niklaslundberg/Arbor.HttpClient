# RX.NET and ReactiveUI Architecture Evaluation

This document evaluates [RX.NET (System.Reactive)](https://github.com/dotnet/reactive) and [ReactiveUI](https://reactiveui.net/) as complementary tools for the feature-separation work tracked in [`clean-feature-separation.md`](clean-feature-separation.md) and [`mainwindowviewmodel-split-plan.md`](mainwindowviewmodel-split-plan.md).

> **Superseded by a later decision** — see [`docs/suggestions/ReactiveUIAdoptionScope.md`](../suggestions/ReactiveUIAdoptionScope.md):
> the project performed a **full ReactiveUI migration** rather than the additive-only approach
> recommended below. This evaluation is kept for historical context.

## What are these libraries?

### RX.NET (System.Reactive)

RX.NET implements the [ReactiveX](https://reactivex.io/) specification for .NET. It models asynchronous event streams as first-class `IObservable<T>` values and provides a rich set of composable operators (`Select`, `Where`, `Merge`, `CombineLatest`, `Throttle`, `Buffer`, etc.) for transforming and combining those streams.

Key concepts relevant to this project:

| Concept | Description |
|---|---|
| `IObservable<T>` | A push-based sequence — the producer pushes items to subscribers |
| `IObserver<T>` / `Subscribe` | Consume items, errors, and completion signals from a stream |
| `Subject<T>` | A hot observable that can be used as a simple event bus |
| `IScheduler` | Abstracts the execution context (thread pool, UI thread, test scheduler) enabling deterministic unit testing of time-dependent code |
| Operators | Composable transformations and combinators on streams |

### ReactiveUI

ReactiveUI is an MVVM framework built on top of RX.NET. It extends the observable model into the ViewModel layer:

| Feature | Description |
|---|---|
| `ReactiveObject` | ViewModel base that wraps `INotifyPropertyChanged` in `IObservable<T>` via `WhenAnyValue` |
| `ReactiveCommand<TIn, TOut>` | An observable-aware command with built-in `CanExecute`, `IsExecuting`, and error-stream properties |
| `WhenAnyValue` | Derives an observable stream from one or more ViewModel properties |
| `WhenAnyObservable` | Composes observables from child VMs reactively |
| `Interaction<TInput, TOutput>` | Observable-based UI interactions (dialogs, confirmations) that are easily testable |
| `ObservableAsPropertyHelper<T>` | Derives a read-only property from an observable stream |

### DynamicData

[DynamicData](https://github.com/reactivemarbles/DynamicData) is a companion library that brings observable change sets to collections (`IObservableCache<T, TKey>`, `IObservableList<T>`). It integrates tightly with ReactiveUI and can replace manual `ObservableCollection<T>` mutation logic.

## How these fit the current project

The project currently uses **CommunityToolkit.Mvvm** with `[ObservableProperty]`/`[RelayCommand]` source generators and **Avalonia** as the UI framework. The split plan in [`mainwindowviewmodel-split-plan.md`](mainwindowviewmodel-split-plan.md) already identified four communication-pattern options (A–D). RX.NET and ReactiveUI add a fifth option that sits between Option B (domain events) and Option D (mediator) in terms of coupling and sophistication.

### Where RX.NET/ReactiveUI adds value in this codebase

#### 1. Cross-feature notifications (replaces/complements Option B)

Features such as **Environments**, **Collections**, and **HttpRequest** need to react when shared state changes (e.g., a selected environment changes → variable substitution must re-run). With raw `INotifyPropertyChanged` this requires property watchers or shared mutable state. With pure `System.Reactive` it becomes:

```csharp
// EnvironmentsViewModel exposes an observable
public IObservable<RequestEnvironment?> SelectedEnvironmentChanged { get; }

// RequestEditorViewModel subscribes without coupling to MainWindowViewModel
environmentsVm.SelectedEnvironmentChanged
    .Subscribe(env => SelectedEnvironment = env)
    .DisposeWith(_disposables);
```

If ReactiveUI is also adopted, `ObservableAsPropertyHelper<T>` can replace the manual setter:

```csharp
// ReactiveUI-specific variant (requires ReactiveObject base)
_selectedEnvironment = environmentsVm.SelectedEnvironmentChanged
    .ToProperty(this, x => x.SelectedEnvironment);
```

This is more composable than a raw C# event and more traceable than an opaque event bus.

#### 2. Scheduled jobs / timers (replaces PeriodicTimer)

`ScheduledJobService` currently uses `PeriodicTimer` (see `src/Arbor.HttpClient.Desktop/Features/ScheduledJobs/ScheduledJobService.cs`). `Observable.Interval` and `Observable.Timer` produce the same pulses but through `IScheduler`, which can be replaced by `TestScheduler` in unit tests:

```csharp
// Production — inject TaskPoolScheduler.Default (or a wrapper interface)
Observable.Interval(_interval, _scheduler)
    .SelectMany(_ => RunJobAsync(CancellationToken.None))
    .Subscribe();

// Test — advance virtual time without Task.Delay pauses
var testScheduler = new TestScheduler();
testScheduler.AdvanceBy(_interval.Ticks);
```

This makes scheduled-job tests deterministic without `Task.Delay` pauses.

#### 3. Async command handling

`ReactiveCommand.CreateFromTask` wraps async operations and exposes `IsExecuting`, `ThrownExceptions`, and the result stream as observables. This can replace some manual `IsBusy` tracking in `MainWindowViewModel`:

```csharp
SendRequest = ReactiveCommand.CreateFromTask(
    execute: cancellationToken => _requestService.SendAsync(Draft, cancellationToken),
    canExecute: this.WhenAnyValue(x => x.IsBusy).Select(busy => !busy));

SendRequest.IsExecuting.BindTo(this, x => x.IsBusy);
SendRequest.ThrownExceptions.Subscribe(ex => Log.Error(ex, "Request failed"));
```

#### 4. Throttled search / auto-complete

The variable completion engine and history search already debounce user input manually. The `Throttle` operator on a `Subject<string>` simplifies this (Rx.NET's `Throttle` implements the debounce semantic — it emits a value only after the source has been silent for the given duration):

```csharp
_searchSubject
    .Throttle(TimeSpan.FromMilliseconds(300), RxApp.TaskpoolScheduler)
    .DistinctUntilChanged()
    .SelectMany(query => _historyRepository.SearchAsync(query, CancellationToken.None))
    .ObserveOn(RxApp.MainThreadScheduler)
    .Subscribe(results => SearchResults = results);
```

#### 5. Observable collections (DynamicData)

`MainWindowViewModel` currently manages request collections via `CollectionGroups : ObservableCollection<CollectionGroupViewModel>`. Once the Collections slice is extracted to a dedicated `CollectionsWorkflowViewModel`, filtering, sorting, and live-updating the list become declarative with DynamicData:

```csharp
_collectionSource.Connect()
    .Filter(this.WhenAnyValue(x => x.FilterText).Select(BuildFilter))
    .Sort(SortExpressionComparer<CollectionGroupViewModel>.Ascending(x => x.Name))
    .ObserveOn(RxApp.MainThreadScheduler)
    .Bind(out _filteredGroups)
    .Subscribe();
```

## Trade-off comparison

| Aspect | CommunityToolkit.Mvvm (current) | RX.NET / ReactiveUI |
|---|---|---|
| **Learning curve** | Low — familiar C# events and attributes | Medium-high — functional/reactive mental model |
| **Boilerplate** | Low via source generators | Low for streams, higher for wiring |
| **Testability** | Good for simple commands | Excellent — `TestScheduler` enables time-travel tests |
| **Cross-feature communication** | Raw events or shared VM references | Observable subscriptions with lifecycle management |
| **Async commands** | `AsyncRelayCommand` | `ReactiveCommand` with built-in `IsExecuting`/`ThrownExceptions` streams |
| **Collection transforms** | Manual `ObservableCollection` mutation | DynamicData operators (filter, sort, flatten) |
| **Debugging** | Straightforward stack traces | Subscription chains can be hard to trace |
| **Bundle size / dependencies** | Minimal | System.Reactive (~2 MB), ReactiveUI (~500 KB), optional DynamicData |
| **Avalonia compatibility** | Native CommunityToolkit support | `ReactiveUI.Avalonia` adapter package exists |
| **Migration cost** | — | High if replacing CommunityToolkit.Mvvm wholesale; low if used additively |

## Compatibility with the existing stack

ReactiveUI ships an [Avalonia integration package](https://www.nuget.org/packages/ReactiveUI.Avalonia) (`ReactiveUI.Avalonia`). It does **not** conflict with CommunityToolkit.Mvvm — both can coexist in the same project.

**Additive usage** (the recommended path for this project): keep `CommunityToolkit.Mvvm` for property generation and introduce `System.Reactive` streams selectively in feature VMs where cross-feature communication, throttling, or scheduler-controlled async adds clear value.

**Full migration** (not recommended now): replace `ReactiveObject` as the ViewModel base, rewrite commands as `ReactiveCommand`, and bind collections through DynamicData. This would require a full rewrite of the Desktop project and conflicts with the incremental slice-extraction plan.

## Recommended approach

Given the current phased split plan and the team's use of CommunityToolkit.Mvvm, the pragmatic recommendation is:

1. **Do not migrate to ReactiveUI as a framework.** Replacing `[ObservableProperty]`/`[RelayCommand]` wholesale adds migration cost without proportional benefit during the ongoing slice-extraction phase.

2. **Introduce `System.Reactive` (RX.NET core) as a lightweight cross-feature event channel.** Create a thin `IFeatureEventBus` (or per-feature observable endpoints) backed by `Subject<T>`, so extracted feature VMs can subscribe to each other without circular references or shared mutable state.

3. **Use `IScheduler` in new time-dependent features.** Any new code that touches `ScheduledJobService`, auto-save timers, or debounced search should accept an `IScheduler` parameter for deterministic unit-test coverage.

4. **Evaluate DynamicData selectively** for list-management features (Collections panel, history list) once those are extracted from `MainWindowViewModel`. Observable change sets simplify filter/sort/group logic that otherwise lives in the VM.

5. **Revisit full ReactiveUI adoption** only if the slice-extraction phase surfaces persistent cross-feature wiring complexity that interface-based Option A + lightweight RX streams (Option E) cannot resolve cleanly.

## Concrete next steps (if Option E is chosen)

| Step | Description |
|---|---|
| Add `System.Reactive` NuGet package | Add `System.Reactive` to `Directory.Packages.props`; verify Apache-2.0 license |
| Add `THIRD_PARTY_NOTICES.md` entry | Document the package per the license-compatibility policy |
| Create `IFeatureEventBus` or per-feature observable endpoints | Define the observable contracts between feature VMs |
| Replace `ScheduledJobService` timer with `Observable.Interval` | Accept `IScheduler` for testability; write time-travel tests |
| Add throttled search via `Subject<string>` | Replace manual timer debounce in search/completion code |
| Evaluate DynamicData for Collections panel | After `CollectionsWorkflowViewModel` slice is extracted |

## Summary

| Option | When to choose |
|---|---|
| **A. Direct interface calls** | Default for all new slices; low risk, fully explicit |
| **B. Domain events (C# events / delegates)** | Fan-out notifications where ≥ 2 features subscribe to the same event |
| **C. Shared state store** | Not recommended now — high conceptual overhead and migration cost for the current stage |
| **E. RX.NET observable streams** | Fan-out with composition needs (throttle, filter, combine), scheduler-controlled async, or rich collection transforms |
| **D. Mediator/command bus** | Re-evaluate if Options A + B + E still produce clear composition friction |

RX.NET (Option E) is a natural evolution of Option B — it provides the same decoupled fan-out model but adds composability, scheduler abstraction, and collection reactivity that become valuable as the number of extracted feature VMs grows.
