# Clean feature separation

This document answers the architecture questions raised in [issue #33](https://github.com/niklaslundberg/Arbor.HttpClient/issues/33) and provides an ordered task list for keeping the UI modular, testable, and easy to extend.

## Scope

- Are view models separated in a scalable way?
- Can components be reused?
- Can new features be added without touching existing files (to a great extent)?
- Is the code easy to test?
- What other architectural questions should we track continuously?

## Findings

- **Monolithic main view model**: `MainWindowViewModel` (~2,500 lines) owns request editing, response rendering, history, collections, environments, options, scheduling, layout persistence, and logging. All UI actions pass through this single type, so responsibilities are tightly coupled.
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

5. **Reuse guidelines**
   - Extract reusable UI controls (request headers list, query parameters list, response body viewer) so they consume feature interfaces instead of the whole main VM.
   - Keep shared models (`SavedRequest`, `RequestEnvironment`, etc.) in `Arbor.HttpClient.Core` so they stay UI-agnostic.

## Additional questions to track continuously

- Are feature boundaries represented in folder/module structure (vertical slices), not only in class names?
- Does every new feature have at least one test that does not require the full UI runtime?
- Can a feature be disabled or replaced without modifying unrelated features?
- Are cross-feature dependencies explicit (constructor interfaces) rather than implicit (shared mutable state)?
- How should state persistence be scoped — per feature vs global auto-save? What is the rollback story if persistence fails?
- What is the extension-point story for future panels (e.g., WebSocket, gRPC, GraphQL)?
- How do we handle threading and cancellation consistently across features (request sending, file watching, scheduled jobs)?
- What accessibility guarantees must each new panel meet (keyboard navigation, contrast, screen-reader labels)?

## Ordered next steps

1. Extract `RequestEditorViewModel` and move request-specific logic out of `MainWindowViewModel` into a dedicated VM; cover with focused unit tests.
2. Apply the same pattern to options and scheduler — one slice per PR (`EnvironmentsViewModel` extraction is complete).
3. Refactor `DockFactory` to consume feature registrations and stop requiring a `MainWindowViewModel` reference.
4. Evaluate whether a mediator/event-bus is needed after 1–2 successful slice extractions.
5. Introduce a DI container (e.g., `Microsoft.Extensions.DependencyInjection`) only when constructor/composition friction remains significant after slice extraction.
