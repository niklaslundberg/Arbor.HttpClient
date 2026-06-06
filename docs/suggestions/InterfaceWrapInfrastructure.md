# Task: Wrap infrastructure services behind interfaces

**Description**
- Identify concrete services used directly in view‑models (clipboard, file system, timers, logger, etc.).
- Define thin interfaces (e.g., `IClipboardService`, `IFileSystem`, `ITimerProvider`).
- Implement adapters that delegate to the real implementations.
- Inject these interfaces via constructor into the relevant feature ViewModels.

**Acceptance Criteria**
1. Interfaces exist in a new folder `src/Arbor.HttpClient.Desktop/Infrastructure/`.
2. All direct calls to static helpers (`Clipboard.SetTextAsync`, `File.WriteAllTextAsync`, `PeriodicTimer`, etc.) in feature VMs are replaced with the injected interface methods.
3. The production DI container (or manual composition) registers the real implementations.
4. Unit tests can provide mock/fake implementations for isolated testing.
5. No functional behavior changes; existing UI tests pass.

**Tests to Create**
- For each wrapped service, a unit test that verifies the adapter forwards calls correctly to the underlying .NET API.
- Example: `ClipboardServiceTests` asserting that `SetTextAsync` invokes `Avalonia.Platform.Clipboard.SetTextAsync`.
- Feature‑VM tests using a fake `IFileSystem` to ensure no real file I/O occurs.