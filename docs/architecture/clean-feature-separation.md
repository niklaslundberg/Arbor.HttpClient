# Clean feature separation review

This document answers the architecture questions raised in the issue and proposes next steps to keep the UI modular, testable, and easy to extend.

## Scope

- Are view models separated in a scalable way?
- Can components be reused?
- Can new features be added without touching existing files (to a great extent)?
- Is the code easy to test?
- What other architectural questions should we track?

## Findings

- **Monolithic main view model**: `src/Arbor.HttpClient.Desktop/ViewModels/MainWindowViewModel.cs` is ~2.5k lines and owns request editing, response rendering, history, collections, environments, options, scheduling, layout persistence, and logging. Most UI actions hang off this single type, so responsibilities are tightly coupled.
- **Child view models are thin proxies**: dockable VMs such as `RequestViewModel`, `ResponseViewModel`, `LeftPanelViewModel`, and `OptionsViewModel` simply forward commands/properties to `MainWindowViewModel`. Reusing them elsewhere still drags the entire main VM with it.
- **Composition is hard-wired**: `App.axaml.cs` creates concrete services and VMs directly, and `DockFactory` constructs all dockables with a `MainWindowViewModel` dependency. Adding any new dock/tool/document requires edits in both `DockFactory` and `MainWindowViewModel` (commands, state, persistence), which blocks feature isolation.
- **Limited reuse boundaries**: UI pieces share `MainWindowViewModel` state instead of feature-specific contracts (e.g., a request editor interface). Reusing the request panel or environment editor in another host window would require carrying the whole main VM and its services.
- **Testability friction**: Because view models talk to concrete repositories, file system watchers, timers, and UI-only services (clipboard, storage provider) directly, unit tests would need heavy fakes or UI threading. Existing tests are end-to-end; there are no focused unit tests for feature logic.

## Recommendations (incremental)

1. **Carve feature view models with clear contracts**
   - Introduce feature-specific VMs (e.g., `RequestEditorViewModel`, `ResponseViewerViewModel`, `EnvironmentPanelViewModel`, `OptionsPanelViewModel`, `SchedulerPanelViewModel`) that own their state and behaviors.
   - Expose only the minimal interfaces each panel needs (e.g., `IRequestEditor`, `IEnvironmentManager`) and have `MainWindowViewModel` compose them rather than implement everything itself.
2. **Modular dock registrations**
   - Let each feature provide a `DockableRegistration` (view, VM factory, initial placement) consumed by `DockFactory`. Adding a new tool/document should mean adding one registration class, not touching the existing ones.
   - Keep dockable state/persistence per feature so layout restore does not require `MainWindowViewModel` awareness of every panel detail.
3. **Move infrastructure behind services**
   - Wrap clipboard, storage provider, file system watcher, and timers behind interfaces injected into the relevant feature VMs. This enables headless testing and reduces Avalonia-specific coupling in business logic.
   - Keep HTTP orchestration in `HttpRequestService` and have the request editor depend on an interface rather than the concrete service.
4. **Testing approach**
   - Add unit tests per feature VM using injected services/fakes; reserve E2E tests for integration. Start with the request editor (URL/headers/body/variables) and scheduler (intervals, enable/disable) once dependencies are interface-based.
   - Introduce small contract tests to ensure feature modules register dockables correctly without the full `MainWindowViewModel`.
5. **Reuse guidelines**
   - Extract reusable UI controls (request headers list, query parameters list, response body viewer) so they consume feature interfaces instead of the whole main VM. This allows hosting them in other windows or dialogs.
   - Keep shared models (e.g., `SavedRequest`, `RequestEnvironment`) in `Arbor.HttpClient.Core` so they stay UI-agnostic.

## Additional questions to track

- How should state persistence be scoped (per feature vs global auto-save)? What is the rollback story if persistence fails?
- What is the plug-in story for future panels (e.g., WebSocket, gRPC, GraphQL)? What extension points are required?
- How do we handle threading and cancellation consistently across features (request sending, file watching, scheduled jobs)?
- What are the performance hotspots (request streaming, large responses) and how will we measure/regress them?
- What accessibility guarantees must each new panel meet (keyboard navigation, contrast, screen reader labels)?

## Suggested next steps (ordered)

1. Extract `IRequestEditorViewModel` and move request-specific logic from `MainWindowViewModel` into a dedicated VM; cover with unit tests.
2. Apply the same pattern to environments/options/scheduler, lifting infrastructure dependencies behind interfaces.
3. Refactor `DockFactory` to consume feature registrations and stop taking a `MainWindowViewModel` dependency.
4. Introduce a lightweight composition root (e.g., `Microsoft.Extensions.DependencyInjection`) to wire services and feature VMs without manual newing in `App.axaml.cs`.
