# MainWindowViewModel Redesign Plan (message-bus target architecture)

## Status

Proposed (2026-06-15). This is a **new, holistic target architecture** that supersedes the
incremental-extraction framing of [`mainwindowviewmodel-split-plan.md`](mainwindowviewmodel-split-plan.md)
for the remaining work. It is delivered **incrementally** — one feature slice per PR, with the
full test suite green at every step (no broad rewrite in a single PR, per
`.github/copilot-instructions.md` §3/§5).

## Why a new plan

The split-plan campaign succeeded at its original goal: `MainWindowViewModel` shrank from
~4,000 to ~2,537 lines, request execution / collections / scheduled jobs / options / layout
logic moved into headless-testable `*Workflow` / `*Coordinator` classes, and `DockFactory` now
composes panels from per-feature registrations.

What it did **not** finish is *state ownership*. Two large slices — **Explorer**
(`ILeftPanelContext`, ~30 members) and **Request** (`IRequestPanelContext`, ~30 members) — were
*decoupled by interface* but their `[Reactive]` state still lives in `MainWindowViewModel`, which
**implements both interfaces**. Main is still 54 `[Reactive]` properties + 43 `[ReactiveCommand]`s.
Those two interfaces are effectively façades over Main; they invert the *compile-time* dependency
but not the *ownership*. Only `LayoutManagementViewModel` actually moved its state out.

This plan finishes the job by moving state into feature VMs and replacing the implicit
"everything routes through Main" coupling with **explicit, typed, UI-agnostic message contracts**.

## Design decisions (locked)

These were settled with the maintainer before writing the plan:

| Decision | Choice |
|---|---|
| **Scope** | Holistic redesign of the Desktop composition, delivered incrementally. |
| **Primary goal** | **Feature independence & testability** — each feature buildable/testable without the full UI runtime; adding a feature touches one folder. |
| **Contract form** | **Both**: (a) concrete feature sub-VMs that *own their own state* (no god-interface), and (b) immutable **Rx message contracts** for cross-feature notifications. |
| **Communication** | A **mediator / message bus**, revisiting the split-plan's earlier rejection (see [Addressing the prior rejection](#addressing-the-prior-rejection)). |
| **Bus implementation** | **Custom, UI-agnostic** — no dependency on Avalonia or ReactiveUI. Lives in `Arbor.HttpClient.Core`. |
| **Command-flow model** | **Hybrid**: Option A (direct `*Coordinator` calls returning immutable outcome records) is the default; Option B (request→handler→response through the mediator) used **selectively** where cross-feature decoupling outweighs the indirection. |
| **DI** | May introduce `Microsoft.Extensions.DependencyInjection`, but **prefer functional code** — pure workflows, immutable records, side effects isolated, minimal mutable state. |

## Target architecture

### 1. The message bus (`Arbor.HttpClient.Core/Messaging`)

A minimal, allocation-light, **UI-framework-agnostic** typed bus. No Avalonia, no ReactiveUI;
depends only on `System.Reactive` (already pinned at 6.1.0 in `Directory.Packages.props`).

```csharp
namespace Arbor.HttpClient.Core.Messaging;

public interface IMessageBus
{
    void Publish<TMessage>(TMessage message);
    IObservable<TMessage> Listen<TMessage>();
}
```

```csharp
public sealed class MessageBus : IMessageBus
{
    private readonly ConcurrentDictionary<Type, object> _subjects = new();

    public void Publish<TMessage>(TMessage message)
        => Subject<TMessage>().OnNext(message);

    public IObservable<TMessage> Listen<TMessage>()
        => Subject<TMessage>().AsObservable();

    private ISubject<TMessage> Subject<TMessage>()
        => (ISubject<TMessage>)_subjects.GetOrAdd(
               typeof(TMessage),
               static _ => Subject.Synchronize(new Subject<TMessage>()));
}
```

**Scheduler discipline:** the bus is scheduler-agnostic. Publishers may publish from any thread;
**UI consumers marshal explicitly** with `.ObserveOn(RxApp.MainThreadScheduler)` at the
subscription site — except flows always initiated on the UI thread (a button click), where the
synchronous handler already runs on the UI thread and no `ObserveOn` is needed. The bus never
touches a UI scheduler, which is what keeps it in `Core` and unit-testable at the consumer.

**Naming note:** ReactiveUI also ships an (unused) `IMessageBus`/`MessageBus`. Because our bus
lives in the ReactiveUI-free `Core` assembly the names are clean there; the Desktop and E2E.Tests
projects add a project-wide `global using` alias (`GlobalUsings.cs`) pointing the unqualified names
at our bus so view models that import ReactiveUI compile without `CS0104` ambiguity.

**Lifetime:** one `IMessageBus` instance per application, owned by the composition root and
injected into feature VMs. Every subscription is `.DisposeWith(...)` a feature-owned
`CompositeDisposable`.

### 2. Message contracts (immutable records)

Cross-feature flows currently mediated implicitly by `MainWindowViewModel` become explicit
records. Initial catalogue (extend per slice — each lives in the *publishing* feature's folder):

| Message (record) | Published by | Consumed by | Replaces today's coupling |
|---|---|---|---|
| `CollectionRequestLoadRequested(Guid CollectionId, CollectionRequest Request)` | Explorer (Collections) | Request panel | `MainWindowViewModel.ApplyCollectionRequestToEditor` reached via `ILeftPanelContext` |
| `HistoryRequestLoadRequested(RequestHistoryEntry Entry)` | Explorer (History) | Request panel | `LoadHistoryRequest` on Main |
| `RequestCompleted(Guid RequestId, int StatusCode)` | Request panel | History (reload), Cookie jar (refresh) | inline calls in `ApplyManualRequestOutcome` |
| `ActiveEnvironmentChanged(IReadOnlyList<EnvironmentVariable> Variables)` | Environments | Request editor / `IVariableAutoCompleteHost`, URL resolution | `ActiveEnvironmentVariables` pulled through Main |
| `AddCurrentRequestToCollectionRequested(...)` | Request panel | Collections | `AddRequestToCollectionCommand` glue in Main |
| `DemoServerStateChanged(bool Running)` | Demo lifecycle | Request panel (banner) | `IsDemoServerBannerVisible` on Main |

Notifications are **fire-and-forget, no reply**. Anything that needs a result is a command
(Option A direct call, or Option B mediator `Send`), never a bus broadcast.

### 3. Feature VMs own their state

The two god-interfaces are **retired**; the left dock decomposes into focused VMs that each own
their `[Reactive]` state and `[ReactiveCommand]`s (the `LayoutManagementViewModel` precedent):

- **`HistoryPanelViewModel`** — `History`, `HistorySearchQuery`, load/filter (wraps the existing
  `RequestHistoryWorkflow`); publishes `HistoryRequestLoadRequested`.
- **`CollectionsExplorerViewModel`** — collections list, selection, new/rename forms, inherited
  headers, search/sort/group/display (wraps `CollectionsWorkflow`,
  `CollectionsManagementCoordinator`, `CollectionFilterWorkflow`,
  `CollectionInheritedHeadersWorkflow`); publishes `CollectionRequestLoadRequested` /
  `AddCurrentRequestToCollectionRequested`.
- **`ScheduledJobsPanelViewModel`** — `ScheduledJobs` + add/remove (wraps `ScheduledJobsWorkflow`).
- **`LeftPanelViewModel`** — becomes a *thin container* composing the three above + the active-tab
  selector; no business state of its own.
- **`RequestPanelViewModel`** — owns `RequestTabs` / `ActiveRequestTab`, the active
  `RequestEditorViewModel` + protocol sub-VMs (GraphQL/WebSocket/SSE/Script), primary-action /
  demo-banner state, and the embedded **response-state projection** (the ~20 `Response*`
  properties now on Main, fed by `HttpResponseProjectionWorkflow`). Publishes `RequestCompleted`;
  consumes `CollectionRequestLoadRequested` / `HistoryRequestLoadRequested`.

`MainWindowViewModel` keeps only: constructing the feature VMs + bus, the dock-tree/window-geometry
pipeline (already established as staying in Main), and top-level app glue (theme/font apply, which
mutate `Application.Current`). Target: well under 1,000 lines, ideally composition-only plus the
documented Avalonia-bound layout pipeline.

### 4. Command-flow model (hybrid)

- **Default — Option A.** One-shot operations call their `*Coordinator` directly and get an
  immutable outcome record back, then project it onto local `[Reactive]` state. This is the proven
  `ManualHttpRequestCoordinator → ManualHttpRequestOutcome` shape; `Task<TOutcome>` is the correct
  type for a request/response, and it stays trivially traceable and testable.

- **Selective — Option B.** For a *cross-feature* command where the caller should not know the
  handler (e.g. "add current request to a collection" issued from the Request panel and handled by
  Collections), define `record Xxx(...) : IRequest<TResult>` + an `IRequestHandler<Xxx, TResult>`
  routed through the mediator. Use this only where the decoupling earns the indirection; document
  the choice in the slice PR.

```csharp
// Option A (default): direct, result returned, then broadcast the fact
var outcome = await _manualHttpRequestCoordinator.SendAsync(draft, cancellationToken);
ApplyOutcome(outcome);
_messageBus.Publish(new RequestCompleted(outcome.RequestId, outcome.StatusCode));

// Option B (selective): caller depends only on the mediator
var result = await _mediator.Send(new AddCurrentRequestToCollection(draft, collectionId), ct);
```

### 5. DI and the functional preference

Introduce `Microsoft.Extensions.DependencyInjection` (MIT — add to `Directory.Packages.props`,
`THIRD_PARTY_NOTICES.md`) at the **composition root only** (`App`/`Program`), registering the bus
(singleton), repositories, services, workflows, and feature VMs. The container replaces hand-wired
`new` chains as the feature count grows — it does **not** leak into feature code.

Functional preferences inside features (unchanged from the established convention, reinforced):

- Workflows are pure where possible — take inputs, return immutable result records, no hidden state.
- Message contracts and outcome types are `sealed record`s.
- Side effects (persistence, file IO, timers, clipboard) live behind injected interfaces in
  coordinators/services, never inline in VMs.
- VM mutable surface is limited to `[Reactive]` UI state; everything else is derived or pure.

## Addressing the prior rejection

`mainwindowviewmodel-split-plan.md` rejected a mediator / `ReactiveUI.MessageBus` as a
service-locator pattern that hides flows. This redesign keeps that critique honest by:

1. **Typed contracts, not strings/objects** — every message is a named record; "find all
   references" locates every publisher and subscriber.
2. **No global locator** — `IMessageBus` is constructor-injected, not resolved from a static. The
   composition root is the only place that knows the concrete `MessageBus`.
3. **UI-agnostic and testable** — the bus lives in `Core` with no UI dependency; consumers
   marshal to the UI thread themselves, so feature logic is `TestScheduler`-testable.
4. **Commands stay direct by default** — request/response keeps its `Task<TOutcome>` shape
   (Option A); the bus is reserved for genuine fan-out, which is exactly where an event channel
   beats threading a result back through Main.

## Incremental delivery — ordered slices

One slice per PR; `dotnet test Arbor.HttpClient.slnx` green throughout; UI slices verified with
the `Category=Screenshots` headless E2E suite (byte-compare before/after, per the established
verification habit).

| # | Slice | Outcome | Risk | Status |
|---|---|---|---|---|
| 0 | **`IMessageBus` in Core** | Bus + `MessageBusTests` land; nothing wired yet. Add `System.Reactive` ref to `Core`. | Very low | **Done** |
| 1 | **DI at the composition root** | Register the *existing* object graph in MS.DI with no behavior change; `App` resolves `MainWindowViewModel` from the container. | Low | **Deferred** — premature while `App.axaml.cs` is one hand-wired chain with stateful closures and a single composed VM; revisit once several feature VMs exist. |
| 2 | **Explorer → History panel VM** | Extract `HistoryPanelViewModel`; History state leaves Main; `HistoryRequestLoadRequested` is the first real message. | Medium | **Done** — Main drops its History members; `LoadHistoryRequest` now publishes the message, Main applies it to the editor on receipt. |
| 4 | **Explorer → Scheduled jobs VM** | `ScheduledJobsPanelViewModel`; options values supplied as delegates, tab-switch via an `onJobAdded` callback. | Medium | **Done** — Main drops its scheduled-jobs members; `ILeftPanelContext` now exposes `ScheduledJobsPanel`. |
| 5 | **Request panel VM** + retire `IRequestPanelContext` | `RequestPanelViewModel` owns tabs/editor/response projection; `RequestCompleted` drives History reload + cookie refresh. | High (response-projection surface) | **In progress** — 5a done: response display state + projection/snapshot logic extracted into `ResponseStateViewModel` (Main delegates and forwards `PropertyChanged`, so views/contracts are untouched for now). Remaining: tabs/editor ownership, then retire the contract and rebind views to `App.Response`. |
| 3 | **Explorer → Collections VM** + retire `ILeftPanelContext` | Extract `CollectionsExplorerViewModel`; `CollectionRequestLoadRequested`; live-preview header sync via message. | Medium-high | **Re-sequenced after slice 5** — Collections' load/add/inherited-header flows are bound to the request tabs + editor, so a clean extraction is blocked until the request panel owns them. |
| 6 | **Environment & demo notifications** | `ActiveEnvironmentChanged` / `DemoServerStateChanged` replace pull-through-Main access. | Medium |
| 7 | **Selective Option B** | Convert the chosen cross-feature command(s) (e.g. add-to-collection) to mediator `IRequest<T>` handlers. | Medium |
| 8 | **Main = composition only** | Remove residual glue; confirm Main is composition + the documented Avalonia layout pipeline. | Low |

Each PR: focused unit tests for the new VM/messages **without** the full UI runtime, the screenshot
verification for any view binding change, and `docs/coverage.md` refreshed.

## Success metrics

- `ILeftPanelContext` and `IRequestPanelContext` deleted; no feature VM implements a god-interface.
- Each feature VM owns its state and has focused tests that don't need the Avalonia runtime.
- `MainWindowViewModel` is composition + layout pipeline only (target < 1,000 lines).
- Adding a new panel touches one feature folder + one composition-root registration line.
- Cross-feature flows are greppable: every interaction is a named record with visible
  publisher/subscriber, or a direct typed coordinator call.

## Risks & controls

- **Response-projection surface (slice 5)** is the riskiest move — ~20 `Response*` properties feed
  the embedded response view. Mitigate by moving the projection wholesale (it already comes from
  `HttpResponseProjectionWorkflow`) and gating on the `state-response` screenshot.
- **Message ordering / re-entrancy** — keep messages coarse (one per user-meaningful event); never
  publish from inside a subscriber to the same message type. Consumers marshal to the UI thread.
- **Bus overuse** — the hybrid rule is the guardrail: if a flow needs a result, it is a command,
  not a message. Review each new message against "is anyone replying?".
- **DI scope creep** — container usage stays at the composition root; reject `IServiceProvider`
  injection into features.

## Non-goals

- No behavior or UX changes beyond the structural move.
- No HTTP/TLS configuration changes.
- No conversion of working command flows to the bus "for consistency" (violates the hybrid rule).
- No big-bang PR.
