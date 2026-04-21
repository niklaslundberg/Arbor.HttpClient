# Architecture Review (Clean Feature Separation)

This review summarizes the current architecture and the main scaling risks observed so far.

## What is already working well

- The solution is split into clear top-level layers:
  - `Arbor.HttpClient.Core` (UI-agnostic logic)
  - `Arbor.HttpClient.Storage.Sqlite` (persistence implementation)
  - `Arbor.HttpClient.Desktop` (Avalonia UI)
- Core abstractions (`IRequestHistoryRepository`, `ICollectionRepository`, `IEnvironmentRepository`, `IScheduledJobRepository`) make storage replaceable and support test doubles.
- Several UI concerns are already separated into dedicated view models (`RequestViewModel`, `ResponseViewModel`, `OptionsViewModel`, `EnvironmentsViewModel`, `ScheduledJobViewModel`, etc.).

## Current architecture risks

- `MainWindowViewModel` is very large and acts as an orchestration point for many features at once (request editing, environments, options, layout, scheduling, import/export, logs). This is the main scalability bottleneck.
- Adding new features often requires touching `MainWindowViewModel`, increasing merge conflicts and coupling between unrelated features.
- Many feature behaviors are currently embedded in private methods on `MainWindowViewModel`, which makes focused unit testing harder than necessary.

## Recommended direction for scalable feature separation

1. Keep `MainWindowViewModel` as a composition root only (window-level coordination and navigation state).
2. Move feature logic behind feature-specific services/coordinators and smaller view models (for example: Request Composer, Environment Management, Scheduled Jobs, Layout Management).
3. Prefer constructor-injected interfaces for new feature logic so behavior can be tested without UI/runtime dependencies.
4. Keep new features additive: new files + new registrations should be the default path, with minimal edits to existing features.

## Assessment against the issue questions

- **Are view models separated in a scalable way?**  
  Partially. There is good initial separation, but `MainWindowViewModel` remains too central for long-term scale.

- **Can components be re-used?**  
  Core and repository abstractions are reusable. Reuse inside Desktop is reduced where logic is embedded directly in `MainWindowViewModel`.

- **Can new features be added without touching existing files (to great extent)?**  
  Not consistently yet. New feature work frequently lands in `MainWindowViewModel`.

- **Is the code easy to test?**  
  Core is test-friendly; UI feature logic is mixed. Extracting feature logic to injected services will improve isolation and test speed.

## Additional architecture questions to ask continuously

- Are feature boundaries represented in folder/module structure (vertical slices), not only in class names?
- Does every new feature have at least one test that does not require the full UI runtime?
- Can a feature be disabled/replaced without modifying unrelated features?
- Are cross-feature dependencies explicit (constructor interfaces) rather than implicit (shared mutable state)?
- Do we track and cap growth of orchestration classes (like `MainWindowViewModel`)?
