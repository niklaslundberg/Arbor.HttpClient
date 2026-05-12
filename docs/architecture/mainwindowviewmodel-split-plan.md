# MainWindowViewModel Split Plan

## Context

`MainWindowViewModel` currently contains multiple feature areas and coordination responsibilities in one class (about 4,000 lines). This slows down feature work, increases regression risk, and makes focused testing harder.

This document is a persisted implementation plan for splitting feature logic from `MainWindowViewModel` while keeping behavior stable.

## Goals

- Keep `MainWindowViewModel` small and focused on composition/orchestration.
- Move feature-specific logic to feature-specific ViewModels/services.
- Improve test isolation with smaller, specialized test classes.
- Make new features additive (new folder/class) instead of requiring broad edits.
- Preserve current UX and persistence behavior during migration.

## Non-goals

- No broad rewrite in one PR.
- No behavior changes unrelated to feature separation.
- No forced DI container adoption unless composition friction remains after slice extraction.

## Current responsibility clusters to extract

Start with these clusters currently owned in `MainWindowViewModel`:

1. **Request execution flow** (HTTP/GraphQL/WebSocket/SSE dispatch and response handling)
2. **Collections workflows** (create/rename/delete/import/add current request)
3. **Scheduled job workflows** (create/start/stop/remove and persistence hooks)
4. **Options import/export and autosave triggers**
5. **Draft persistence and restore loop**
6. **Demo server start/stop/seed lifecycle**
7. **Response export/copy/open-in-editor actions**

## Target structure

## `Arbor.HttpClient.Desktop/Features/Main`

- `MainWindowViewModel` (composition only)
- `IMainWindowFeatureCoordinator` (optional minimal abstraction for orchestration)

## `Arbor.HttpClient.Desktop/Features/*`

- Feature-scoped orchestrators where logic currently lives in Main:
  - `HttpRequestWorkflowViewModel` (or coordinator)
  - `CollectionsWorkflowViewModel`
  - `ScheduledJobsWorkflowViewModel`
  - `ApplicationOptionsWorkflowViewModel`
  - `DraftWorkflowViewModel`
  - `DemoServerWorkflowViewModel`
  - `ResponseActionsViewModel`

Each extracted unit should own:

- Its own state + commands
- Its own dependencies (constructor-injected)
- Its own focused tests

`MainWindowViewModel` should only:

- Construct/receive feature VMs
- Route top-level commands to feature VMs
- Keep cross-feature composition glue minimal and explicit

## Phased execution plan

### Phase 1 — Define boundaries and contracts (small PR)

- Introduce focused interfaces for each extraction candidate where needed.
- Keep existing behavior paths unchanged.
- Add adapter/proxy members in `MainWindowViewModel` to avoid XAML churn in the same PR.

**Exit criteria:** boundaries compile, no UX changes, tests unchanged.

### Phase 2 — Extract one vertical slice at a time (repeated small PRs)

Recommended extraction order (lowest coupling first):

1. Response actions (copy/save/open external)
2. Demo server lifecycle
3. Collections workflows
4. Scheduled jobs workflows
5. Request execution orchestration
6. Draft/options autosave orchestration

For each slice:

- Move logic to a feature VM/coordinator
- Keep old member signatures in `MainWindowViewModel` delegating to the new unit
- Add/adjust focused tests for the extracted unit

**Exit criteria per slice:** behavior parity, targeted tests pass, no unrelated file churn.

### Phase 3 — Remove temporary delegation surface

- Remove obsolete pass-through members from `MainWindowViewModel` after XAML and dependent code bind directly to feature VMs.
- Keep only composition/orchestration concerns in Main.

**Exit criteria:** Main is orchestration-only; no duplicate command paths.

### Phase 4 — Hardening and guardrails

- Add architecture checks to PR review habits:
  - “No new feature logic in MainWindowViewModel.”
  - “New feature requires at least one focused unit test not requiring full UI runtime.”
- Keep feature-folder placement strict.

**Exit criteria:** new feature additions are mostly isolated to one feature folder plus composition wiring.

## Test split plan

Current large classes should be incrementally split by behavior area.

### MainWindow UI tests

Split `MainWindowUiTests` into focused classes, for example:

- `MainWindowLayoutUiTests`
- `MainWindowCollectionsUiTests`
- `MainWindowScheduledJobsUiTests`
- `MainWindowRequestExecutionUiTests`
- `MainWindowOptionsUiTests`

### Request editor tests

Split `RequestEditorViewModelTests` by behavior area, for example:

- `RequestEditorHeadersTests`
- `RequestEditorQueryParametersTests`
- `RequestEditorBodyFormattingTests`
- `RequestEditorDraftPersistenceTests`

### Test design rules during split

- Keep test classes small and single-purpose.
- Prefer many focused test classes over one giant class.
- Keep integration tests explicitly categorized and isolated from pure unit tests.

## Communication pattern options between modules

| Option | Description | Pros | Cons | Impact | Recommendation |
|---|---|---|---|---|---|
| A. Direct interface calls (default) | Feature VM/coordinator calls another via small interface contracts | Simple, explicit, easy to debug, minimal infrastructure | Can grow coupling if interfaces become broad | Low migration cost, low risk | **Start here** |
| B. Domain events / event aggregator | Features publish events (`RequestSent`, `EnvironmentChanged`) consumed by subscribers | Decouples producers/consumers, good for cross-feature notifications | Harder tracing, risk of hidden flows, ordering complexity | Medium cost, medium risk | Use only where multiple subscribers exist |
| C. Shared state store (single source of truth) | Central immutable-ish state + reducers/actions | Predictable state transitions, easier time-travel/debug tooling | Higher conceptual overhead, boilerplate, broad migration | High cost, medium risk | Not needed now |
| D. Mediator/command bus | Commands routed through mediator handlers | Strong separation, extensible for plugins/modules | Indirection, can hide ownership, overkill early | Medium-high cost | Re-evaluate after 1–2 successful extractions |

### Practical recommendation

- Use **Option A** as baseline for first extractions.
- Introduce **Option B** only for true fan-out notifications.
- Reassess mediator/event-bus only if direct contracts start causing clear composition friction.

## Risk controls

- Migrate in small PRs with one slice per PR.
- Preserve existing command/property names temporarily via delegation to reduce UI break risk.
- Validate each extraction with targeted tests first, then full suite.
- Avoid simultaneous refactor of multiple feature slices.

## Success metrics

- `MainWindowViewModel` reduced substantially and mainly orchestration/composition.
- New feature PRs touch primarily one feature folder + minimal composition wiring.
- Tests are split into smaller specialized classes by feature area.
- Cross-feature communication is explicit and intentional.

## Definition of done for this issue

- This plan is persisted in the repository.
- Future implementation PRs should reference this document and execute it incrementally.
