# PR Review Checklist

This checklist collects review issues that have appeared in multiple pull requests. Apply these checks before every PR is marked ready for review.

## CodeQL / Static Analysis

These findings have recurred across multiple PRs and are caught by the CodeQL security workflow. Address them before pushing to avoid post-review fix commits.

### Dispose IDisposable locals

Wrap every locally created `IDisposable` in a `using` declaration or statement so it is disposed even when an exception is thrown.

**Recurring locations:** `HttpResponseMessage` instances in test methods, `StreamReader`, `MemoryStream`.

```csharp
// ✗ Triggers CodeQL: CA2000 / Missing Dispose call on local IDisposable
var response = new HttpResponseMessage();

// ✓ Correct
using var response = new HttpResponseMessage();
```

### Use `.Where()` instead of implicit filtering in `foreach`

When a `foreach` loop filters its target sequence with an inner `if` condition, replace the `if` with a `.Where()` call on the collection. This makes the intent explicit and avoids the CodeQL "Missed opportunity to use Where" diagnostic.

```csharp
// ✗ Triggers CodeQL: implicit filter inside foreach
foreach (var item in collection)
{
    if (item.IsActive)
    {
        Process(item);
    }
}

// ✓ Correct
foreach (var item in collection.Where(x => x.IsActive))
{
    Process(item);
}
```

### Mark fields `readonly` when not mutated after construction

Fields that are only assigned in the constructor and never mutated should be declared `readonly`. CodeQL reports this as "Missed 'readonly' opportunity".

```csharp
// ✗ Triggers CodeQL
private SomeService _service;

// ✓ Correct
private readonly SomeService _service;
```

### Threading and async safety checks

Reviewers should explicitly verify these patterns in every PR:

- UI updates are dispatched via `Dispatcher.UIThread.InvokeAsync`/`Post` only when needed. Prefer `Dispatcher.UIThread.CheckAccess()` to avoid unnecessary UI-thread hops.
- Avoid `async void` except for event handlers that cannot be `Task`-returning.
- Async call chains pass `CancellationToken cancellationToken` through to downstream I/O.
- Avoid sync-over-async (`.Result`, `.Wait()`, `GetAwaiter().GetResult()`) in production code.

## UI Pull Requests

Before merging any PR that touches UI code or theme resources:

- [ ] All new/changed color pairs have been verified with the contrast-ratio formula in `AccessibilityContrastTests.cs` and meet WCAG AA (≥ 4.5:1 for normal text, ≥ 3:1 for large text).
- [ ] Interactive elements remain keyboard-accessible (Tab, Enter/Space, arrow keys where applicable).
- [ ] No purely visual text label has been replaced by an icon without an accessible name.
- [ ] New interactive controls have been manually verified with keyboard-only navigation.
- [ ] E2E screenshot tests have been run and output committed to `docs/screenshots/`.

## Security

- [ ] No secrets, tokens, or credentials committed.
- [ ] No new HTTP/TLS configuration that downgrades security.
- [ ] No sensitive data logged (credentials, PII, raw request bodies).
- [ ] `persist-credentials: false` retained on `actions/checkout` steps.
- [ ] Vulnerability audit (`dotnet list Arbor.HttpClient.slnx package --vulnerable --include-transitive`) passes with no findings.

## Dependencies

- [ ] New NuGet packages have a license compatible with MIT (see [license policy](.github/copilot-instructions.md#license-compatibility)).
- [ ] New packages are declared in `Directory.Packages.props` (not inline in `.csproj`).
- [ ] New packages are documented in `THIRD_PARTY_NOTICES.md`.

## Code Structure Consistency

- [ ] New files are placed in the correct feature folder (vertical slice) rather than a type-based folder.
  - `Arbor.HttpClient.Core`: feature folders at root (e.g. `HttpRequest/`, `Collections/`, `Environments/`) — no `Models/`, `Services/`, or `Abstractions/` directories.
  - `Arbor.HttpClient.Desktop`: feature folders under `Features/` (e.g. `Features/HttpRequest/`, `Features/Logging/`) — no top-level `ViewModels/`, `Views/`, `Converters/`, `Models/`, or `Services/` directories.
- [ ] New feature code lives entirely in its own folder — no edits to existing features required.
- [ ] No dead code introduced; every new type is reachable from the application entry point or from tests (the compiler alone may not detect reflection-based or XAML-only usage — check manually).

## General

- [ ] All tests pass (`dotnet test Arbor.HttpClient.slnx`).
- [ ] No compiler warnings introduced.
- [ ] No unrelated files modified.
- [ ] PR description explains *what* changed and *why*.
- [ ] Code coverage maintained or improved (check CI job summary for coverage report).
- [ ] `release.yml` changes mirrored in the `release-verification` job in `ci.yml` (excluding `Attest build provenance` and `Create GitHub Release`).

## UX Ideas Maintenance

> See `.github/copilot-instructions.md` section 13 and `.github/prompts/ux-review.prompt.md` for the full workflow.

- [ ] `docs/ux-ideas.md` reviewed for this PR's changes.
- [ ] Implemented ideas moved from "Not Yet Implemented" to the "Implemented" section with PR number, commit SHA, and file reference.
- [ ] New UX ideas discovered during this PR added to "Not Yet Implemented" (if any).

## Instruction Improvement Loop

> See `.github/copilot-instructions.md` section 14.

- [ ] Instruction Retrospective block included in the PR description.
- [ ] Proposed instruction improvement applied to `.github/copilot-instructions.md` (or tracked as a GitHub issue with a link in the retrospective).

## Full Compliance Checklist

Use `.github/prompts/pr-checklist.prompt.md` to generate the complete checklist (paste output into the PR description). The items above are the most commonly missed; the prompt covers every blocking and required item from all docs.
