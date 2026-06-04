# Task: Refactor DockFactory to use feature registrations

**Description**
- Replace the current `DockFactory` implementation that directly references `MainWindowViewModel` with a registration‑based approach.
- Each feature provides a lightweight `DockRegistration` object containing the view type, view‑model factory, and default placement.
- `DockFactory` iterates over the registrations to build the dock layout.

**Acceptance Criteria**
1. A new interface `IDockRegistration` exists with properties `Type ViewType`, `Func<object> ViewModelFactory`, and optional `DockLocation`.
2. Every feature folder (`Features/*`) contains a static class exposing one or more `IDockRegistration` implementations.
3. `DockFactory` no longer creates feature view‑models directly; it composes them from the registrations.
4. Adding a new panel only requires adding a registration class – no changes to `DockFactory` itself.
5. All existing UI tests (`MainWindowUiTests`) pass.

**Tests to Create**
- Unit test for `DockFactory` verifying that given a list of registrations it creates the expected number of dock panels.
- Integration test that a newly added dummy feature registration appears in the UI without modifying `DockFactory`.
- Verify that the old `MainWindowViewModel` reference is removed from the factory’s constructor signature.