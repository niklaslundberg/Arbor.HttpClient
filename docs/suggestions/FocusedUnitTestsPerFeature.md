# Task: Add focused unit tests for each extracted feature ViewModel

**Description**
- After feature ViewModels are extracted, each should have its own suite of unit tests that verify its behavior in isolation.
- Tests must use the in‑memory fakes provided in `Arbor.HttpClient.Testing` (e.g., `FakeSystemEnvironmentVariableProvider`).
- Avoid UI runtime dependencies; tests should run in a plain .NET process.

**Acceptance Criteria**
1. Every feature folder (`Features/*`) contains a corresponding test file in the appropriate test project (`*.Tests`).
2. Each test class has at least one test covering the primary command or state change of the feature.
3. Tests use only injected fakes and never touch the Avalonia UI thread.
4. Test projects compile and all tests pass (`dotnet test`).

**Tests to Create**
- Example: `RequestEditorViewModelTests.cs` with tests for URL validation, header manipulation, and draft persistence.
- Example: `EnvironmentPanelViewModelTests.cs` verifying that changing the selected environment updates dependent variables.
- Example: `ScheduledJobsWorkflowViewModelTests.cs` using a fake `ITimerProvider` to simulate job ticks.
- Add a test runner verification step in CI that ensures the number of test classes equals the number of feature folders (optional).