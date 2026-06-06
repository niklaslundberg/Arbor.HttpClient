# Copilot Instructions

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 0. Read All Markdown Files First

**At the start of every session, read all Markdown files in the repository before making any decisions or changes.**

| File | Purpose |
|------|---------|
| `README.md` | Project overview and quick-start |
| `docs/ux-ideas.md` | UX enhancement backlog with scope estimates |
| `docs/review-checklist.md` | Common CodeQL / security / UI review items to check before every PR |
| `docs/security-review.md` | Security posture, findings, and guidelines for future PRs |
| `docs/coding-guideline-suggestions.md` | Historical planning record — all items now incorporated into this file `[OPTIONAL]` |
| `docs/coverage.md` | Code coverage baseline, targets, and CI integration — **single source of truth for coverage numbers** |
| `docs/architecture/clean-feature-separation.md` | Architecture decisions, findings, and ordered next steps |
| `THIRD_PARTY_NOTICES.md` | Third-party dependency attribution |
| `.github/copilot-instructions.md` | This file |

## Repository Constants

| Constant | Value |
|---|---|
| Solution file | `Arbor.HttpClient.slnx` |
| Test command | `dotnet test Arbor.HttpClient.slnx` |
| Vulnerability audit command | `dotnet list Arbor.HttpClient.slnx package --vulnerable --include-transitive` |
| Core project | `src/Arbor.HttpClient.Core` |
| Desktop project | `src/Arbor.HttpClient.Desktop` |
| Storage project | `src/Arbor.HttpClient.Storage.Sqlite` |
| Test doubles | `src/Arbor.HttpClient.Testing` |
| Screenshot E2E test filter | `--filter "Category=Screenshots"` |
| Screenshot output env var | `SCREENSHOT_OUTPUT_DIR=docs/screenshots` |
| Coverage baseline file | `docs/coverage.md` |

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them — don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it — don't delete it.
- Remove imports/variables/functions that YOUR changes made unused; don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan with a verify step for each.

## 5. All Tests Must Pass Before Committing

**Never commit code that breaks the test suite.**

- Run `dotnet test Arbor.HttpClient.slnx` and confirm no failures before every commit.
- **Exception**: Analysis-only, planning, and informational requests that produce no file changes are exempt.
- Pre-commit hook available: run `./scripts/install-hooks.sh` once after cloning.
- `git commit --no-verify` is for WIP branches only. Never push to `main` with failing tests.
- If a pre-existing test was already failing before your changes, note it in the PR description.

### CI Parity — Run the Same Checks Locally Before Committing

| CI job | Local equivalent |
|--------|-----------------|
| **Restore** | `dotnet restore Arbor.HttpClient.slnx --locked-mode` |
| **Vulnerability audit** | `dotnet list Arbor.HttpClient.slnx package --vulnerable --include-transitive` |
| **Build** | `dotnet build Arbor.HttpClient.slnx --no-restore --configuration Release` |
| **Unit tests** | `dotnet test src/Arbor.HttpClient.Core.Tests/Arbor.HttpClient.Core.Tests.csproj --no-restore --configuration Release` |
| **E2E tests** | `dotnet test src/Arbor.HttpClient.Desktop.E2E.Tests/Arbor.HttpClient.Desktop.E2E.Tests.csproj --no-restore --configuration Release` |
| **UX screenshots** | `./scripts/take-screenshots.sh` *(UI changes only — commits to `docs/screenshots/`)* |
| **Agent instructions sync** | PowerShell: `Compare-Object (Get-Content CLAUDE.md \| Select-Object -Skip 1) (Get-Content AGENTS.md \| Select-Object -Skip 1)` |

## 6. Runtime Validation Before PR Ready

**Run the application headlessly and fix every console error before the PR is ready.**

- Run headlessly via `HeadlessUnitTestSession` or `dotnet run` with output captured.
- Treat any `[Binding]` error, `[Control]` error, or unhandled exception as a defect to fix.
- Exception: Dock framework null-binding noise from the Dock library's own XAML templates (e.g. `DockCapabilityOverrides.CanClose`) cannot be fixed without patching upstream; document these in the PR.
- Re-run after every fix until output is clean. Include captured output as evidence in the PR.

## 7. Code Quality Requirements for New Work

- Treat compiler warnings, analyzer warnings, and runtime errors as real defects. Never silently suppress — add a code comment with justification when suppression is necessary.
- **Analyzer severity policy**: Promote correctness/security Roslyn/CA rules to `warning` or `error`. Keep style-only rules as `suggestion`.
- **Code coverage requirements**:
  - **[REQUIRED]** Coverage numbers must be sourced from an actual `dotnet test --collect:"XPlat Code Coverage"` run. Never estimate.
  - **[REQUIRED]** `docs/coverage.md` is the single source of truth. Do not repeat percentages elsewhere — link to it.
  - New code must not lower the overall coverage percentage.
  - **Coverage targets per project (enforced per PR):**
    - `Arbor.HttpClient.Core`: **100% line coverage** for new/changed classes. Document untestable paths (e.g. real network) explicitly.
    - `Arbor.HttpClient.Desktop`: **90% line coverage** for new/changed ViewModels and services. Exempt UI-thread dispatch paths that can't run headless; document exemptions.
    - `Arbor.HttpClient.Storage.Sqlite`: **90% line coverage** for new/changed repositories.
    - `Arbor.HttpClient.Testing`: no minimum.
  - After every feature PR, run coverage, read the XML, update `docs/coverage.md`, and include numbers in the PR description.
- **Test naming**: `Method_Scenario_ExpectedResult` (e.g. `Parse_EmptyInput_ThrowsArgumentException`). One behavioral intent per test; arrange data explicitly.
- **[REQUIRED][QUALITY]** Avoid single-character or abbreviated variable names in production code.
- **[RECOMMENDED][QUALITY]** Prefer C# pattern matching and logical patterns (`and`, `or`, property patterns) for readability.
- **[RECOMMENDED][ARCHITECTURE]** Prefer pure helper functions over mutating shared state; keep side effects explicit.
- **[REQUIRED][QUALITY]** Keep cyclomatic complexity low: small focused methods, guard clauses, extracted helpers over deep nesting.
- **[REQUIRED][QUALITY]** Integration tests (real file system, network, env vars, database) must be annotated `[Trait("Category", "Integration")]`. Tests that mutate process-global state (e.g. `Environment.SetEnvironmentVariable`) must use a dedicated xUnit collection with `DisableParallelization = true`:
  ```csharp
  [CollectionDefinition("ProcessEnvironment", DisableParallelization = true)]
  public sealed class ProcessEnvironmentCollection;

  [Collection("ProcessEnvironment")]
  [Trait("Category", "Integration")]
  public class MyIntegrationTests { ... }
  ```
  Always save and restore mutated global state in a `finally` block.
- Profiling required for hot paths (per-request, per-item, recurring timer) or code introducing disposable/resource-heavy objects. Use dotMemory Unit, `dotnet-counters`, or BenchmarkDotNet; attach evidence in the PR.
- **UI changes**: run `./scripts/take-screenshots.sh`, commit `docs/screenshots/*.png`, and embed inline in the PR description using relative markdown image paths. Do not save screenshots only to `/tmp/` or outside the repository.

## 8. Public API Change Policy

- Document and review public API changes, especially in `Arbor.HttpClient.Core`.
- Surface breaking changes (removed members, signature changes, changed semantics) in the PR description.

## 9. Async and Cancellation Conventions

- Every async method that performs I/O or can block must accept `CancellationToken cancellationToken` (not `ct`).
- Pass the token to all downstream async calls unless there is a documented reason not to.
- Avoid `async void` except for event handlers that cannot return `Task`.
- Avoid sync-over-async (`.Result`, `.Wait()`, `GetAwaiter().GetResult()`) in production code.
- In UI code, check `Dispatcher.UIThread.CheckAccess()` before dispatching.

## 9a. Date and Time Parsing Conventions

- **[REQUIRED][QUALITY]** Always pass `CultureInfo.InvariantCulture` (never `null`) to `DateTimeOffset.TryParse`, `DateTimeOffset.Parse`, `DateTime.TryParse`, and similar overloads. Passing `null` silently falls back to thread culture and breaks on non-English systems.
  - Bad: `DateTimeOffset.TryParse(value, null, DateTimeStyles.RoundtripKind, out var parsed)`
  - Good: `DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)`
- **[REQUIRED][QUALITY]** Call `.ToUniversalTime()` after parsing a stored UTC timestamp (e.g. from a SQLite `TEXT` column) to normalise the offset.

## 9b. LINQ Conventions

- **[REQUIRED][QUALITY]** Use `.Any(predicate)` instead of a `foreach + if + return true/false` existence check (triggers CodeQL "Missed opportunity to use Where").
- **[REQUIRED][QUALITY]** Use `.Where(predicate)` instead of an `if` inside a `foreach` when filtering a collection.
- **[REQUIRED][QUALITY]** Combine nested `if` statements into a single compound condition when there is no intermediate logic between them (triggers CodeQL "Nested 'if' statements can be combined").

## 10. Logging and Observability Conventions

- Structured logging fields: `requestId`, `environment`, `jobId`, `statusCode`, `durationMs`.
- `Information` — routine events. `Warning` — unexpected but recoverable. `Error` — failures requiring attention.
- Do not log sensitive data (credentials, PII, raw request bodies).

## 11. Exception-Handling Conventions

- Prefer domain-specific exception types at subsystem boundaries.
- Use bare `throw;` when rethrowing — never `throw ex;`.
- Catch only exceptions you can handle; let the rest propagate.
- **[REQUIRED][QUALITY]** Never use `catch {}` or unfiltered `catch (Exception) {}`. Use `catch (Exception ex) when (ex is X or Y)` to enumerate concrete expected types. Bare catches trigger CodeQL `cs/catch-of-all-exceptions`.
- **[REQUIRED][QUALITY]** Remove `try/catch` wrappers entirely when the enclosed code cannot realistically throw.

## 12. Path-Handling Conventions

- **[REQUIRED][QUALITY]** Use `Path.Join` instead of `Path.Combine` whenever any argument is not a compile-time literal. `Path.Combine` silently discards earlier arguments when a later one is rooted — CodeQL `cs/path-combine-user-controlled`. `Path.Join` never drops arguments.
  - Bad: `Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db")`
  - Good: `Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}.db")`
  - `Path.Combine` is acceptable only when **all** arguments are compile-time literals.

## 13. Severity and Category Tags

Prefix instructions and checklist items with these tags so priority is unambiguous.

| Severity tag | Meaning |
|---|---|
| `[BLOCKING]` | Must be satisfied before merge. Hard stop. |
| `[REQUIRED]` | Must be satisfied; skip only with documented justification. |
| `[RECOMMENDED]` | Should be satisfied; deviations acceptable with a brief note. |
| `[OPTIONAL]` | Nice to have. No justification needed if omitted. |

| Category tag | Concern |
|---|---|
| `[SECURITY]` | Credentials, TLS, injection risk |
| `[QUALITY]` | Tests, warnings, analyzer findings |
| `[PROCESS]` | Workflow steps, PR lifecycle, docs |
| `[ACCESSIBILITY]` | WCAG, keyboard navigation, screen-reader labels |
| `[ARCHITECTURE]` | Separation of concerns, dependency direction |
| `[PERFORMANCE]` | Hot paths, memory leaks, profiling |

At the end of every PR, verify all `[BLOCKING]` items in the compliance checklist. For `[REQUIRED]` items, confirm compliance or record a justification.

## 14. PR Task: UX Ideas Maintenance `[REQUIRED][PROCESS]`

**Every PR must keep `docs/ux-ideas.md` current.**

1. Read every idea.
2. Move **fully or partially implemented** ideas from "Not Yet Implemented" to "Implemented" with PR number, short commit SHA, and file reference.
3. Add any **new UX ideas** discovered during this PR to "Not Yet Implemented".
4. Never delete ideas from either list.

Format for implemented entries:
```markdown
### 1.1 Feature Name ✅ Implemented
> Implemented in PR #42 (commit `a1b2c3d`) — `src/path/to/Feature.cs`
```

## 15. PR Task: Instruction Improvement Loop `[RECOMMENDED][PROCESS]`

**After every PR, propose at least one instruction improvement.**

Include a brief "Instruction Retrospective" in the PR description covering:
- What was done and which instruction sections were applied
- Ambiguities encountered (in instructions or user requests)
- What caused rework
- A proposed improvement, or "None — instructions were clear"

Apply self-contained improvements directly to `.github/copilot-instructions.md` in the same PR. If the change affects a `[BLOCKING]` rule or architecture decision, open a GitHub issue instead.

## 16. End-of-PR Compliance Checklist `[BLOCKING][PROCESS]`

**Include the completed checklist in every PR description before marking it ready for review.**

```markdown
## PR Compliance Checklist

### From docs/review-checklist.md
- [ ] **[BLOCKING]** All tests pass (`dotnet test Arbor.HttpClient.slnx`)
- [ ] **[BLOCKING]** No secrets or credentials committed
- [ ] **[BLOCKING]** No compiler warnings introduced
- [ ] **[REQUIRED]** CodeQL / static analysis findings addressed
- [ ] **[REQUIRED]** UI PRs: screenshot evidence in docs/screenshots/ and embedded in PR description *(skip if no .axaml or ViewModel files changed)*
- [ ] **[REQUIRED]** UI PRs: accessibility contrast tests updated *(skip if no .axaml or App.axaml colors changed)*
- [ ] **[REQUIRED]** New NuGet packages: license verified, THIRD_PARTY_NOTICES.md updated, Directory.Packages.props updated

### From docs/security-review.md
- [ ] **[BLOCKING]** No HTTP/TLS configuration downgrade
- [ ] **[BLOCKING]** No sensitive data logged
- [ ] **[REQUIRED]** Vulnerability audit passes (`dotnet list Arbor.HttpClient.slnx package --vulnerable --include-transitive`)
- [ ] **[REQUIRED]** persist-credentials: false retained on actions/checkout

### From docs/ux-ideas.md
- [ ] **[REQUIRED]** docs/ux-ideas.md reviewed; implemented ideas moved to "Implemented" with PR/commit references
- [ ] **[RECOMMENDED]** New UX ideas added to "Not Yet Implemented"

### From docs/architecture/clean-feature-separation.md
- [ ] **[RECOMMENDED]** No new logic added to MainWindowViewModel that belongs in a feature VM
- [ ] **[RECOMMENDED]** New features have at least one focused unit test not requiring the full UI runtime
- [ ] **[REQUIRED]** Test project boundaries respected: no cross-layer project references added to existing test projects

### From section 17 (License Compatibility)
- [ ] **[REQUIRED]** New packages have a compatible license (MIT, Apache-2.0, BSD, ISC, OFL-1.1) *(skip if no new packages)*
- [ ] **[REQUIRED]** New packages declared in Directory.Packages.props, not inline in .csproj *(skip if no new packages)*

### From section 18 (MSIX Packaging)
- [ ] **[REQUIRED]** release.yml changes mirrored in release-verification job in ci.yml *(skip if no workflow changes)*
- [ ] **[REQUIRED]** Shared workflow logic in scripts/, not duplicated inline *(skip if no workflow changes)*

### From section 19 (Accessibility)
- [ ] **[REQUIRED]** New color pairs in App.axaml covered by AccessibilityContrastTests.cs *(skip if no theme color changes)*
- [ ] **[REQUIRED]** Interactive controls keyboard-accessible *(skip if no UI changes)*

### Instruction Improvement Loop (section 15)
- [ ] **[RECOMMENDED]** Instruction Retrospective block written in PR description
- [ ] **[RECOMMENDED]** Proposed improvement applied (or tracked as a GitHub issue)

### Final self-check
- [ ] **[BLOCKING]** Every changed line traces directly to the user's request (no unrelated edits)
- [ ] **[REQUIRED]** PR description explains what changed and why
```

---

*Behavioral guidelines adapted from [vlad-ko/claude-wizard](https://github.com/vlad-ko/claude-wizard), used under the [MIT License](https://github.com/vlad-ko/claude-wizard/blob/main/LICENSE) (Copyright 2026 Vlad Ko).*

<a id="license-compatibility"></a>
## 17. License Compatibility

This project is licensed under the **MIT License**. Verify new NuGet packages are compatible before adding them.

### Compatible licenses (permitted)
- **MIT**, **Apache-2.0** (requires attribution), **BSD-2-Clause / BSD-3-Clause**, **ISC**, **OFL-1.1** (fonts only)

### Incompatible or restricted licenses (requires review)
- **GPL-2.0/3.0**, **LGPL-2.1/3.0** (conditional only), **AGPL-3.0**, **SSPL**, **Proprietary/Commercial**, **No license specified**

### Steps when adding a new NuGet package

1. Check the license on [NuGet.org](https://www.nuget.org) or the source repository.
2. Confirm it is in the **Compatible** list.
3. Add an entry to `THIRD_PARTY_NOTICES.md`: package name/version, authors/copyright, project URL, SPDX license identifier, note if test-only.
4. Declare the version in `Directory.Packages.props` (Central Package Management). Do **not** add `Version` in any `.csproj`.

### Dependency governance
- Monthly review via Dependabot. Triage CVEs within 7 days (direct) / 30 days (transitive).
- Prefer actively maintained packages. Avoid duplicating BCL or existing project functionality.

## 18. MSIX Packaging and Releases

The desktop application is distributed as a signed MSIX. The manifest template is at `src/Arbor.HttpClient.Desktop/packaging/AppxManifest.xml`; `VERSION_PLACEHOLDER` is substituted with `1.0.{run_number}.0` at build time.

The release workflow (`.github/workflows/release.yml`) runs on push to `main` and `workflow_dispatch`: builds, tests, publishes `win-x64` self-contained, generates logo assets, packages with `makeappx.exe`, signs with `signtool.exe`, and creates a GitHub Release.

### Rules when modifying the desktop app

- Keep `AppxManifest.xml` consistent: identity `NiklasLundberg.ArborHttpClient`, Publisher `CN=Arbor.HttpClient`.
- Changing `ProcessorArchitecture` requires matching `-r` change in `dotnet publish`.
- `Publisher` must exactly match the signing certificate `Subject`.
- Required logo sizes: 44×44, 150×150, 310×150, 50×50, 620×300.
- **`[RECOMMENDED][PROCESS]`** Include `workflow_dispatch` and handle existing releases idempotently.
- **`[REQUIRED][PROCESS]`** `ci.yml` must contain a `release-verification` job mirroring every `release.yml` step except `Attest build provenance` and `Create GitHub Release`. Keep them in sync.
- **`[REQUIRED][PROCESS]`** Never duplicate workflow logic — extract shared steps to `scripts/` and invoke from both workflows.
- **`[RECOMMENDED][PROCESS]`** Pass `-m <build-drop-path>` to `sbom-tool`; it appends `_manifest` automatically — do not pre-append it.

## 19. Accessibility

All UI changes must consider accessibility from the start.

- **Color contrast**: WCAG 2.1 Level AA — ≥ 4.5:1 for normal text, ≥ 3:1 for large text and UI components.
- **Theme consistency**: Define colors per-theme (Dark/Light) in `ResourceDictionary.ThemeDictionaries` in `App.axaml`.
- **Contrast tests**: Every new color pair in `App.axaml` must have a test in `AccessibilityContrastTests.cs`.
- **Keyboard navigation**: Interactive controls must be reachable and operable by keyboard alone.
- **Screen reader labels**: Non-decorative icons/images must have `AutomationProperties.Name`.

Verification checklist for UI PRs:
- [ ] New/changed color pairs verified and meet WCAG AA.
- [ ] Interactive elements remain keyboard-accessible.
- [ ] No visual label replaced by an icon without an accessible name.
- [ ] New interactive controls verified with keyboard-only navigation.

## 20. UI Consistency

Text inputs replacing a standard `TextBox` (e.g. `AvaloniaEdit.TextEditor` for URL variable highlighting) must match Fluent `TextBox` metrics:

- `Padding="5,6,5,6"` on the editor; `CornerRadius="3"` on the surrounding `Border`
- `Background="{DynamicResource SurfaceBackgroundBrush}"` on the `Border`; `Background="Transparent"` on the inner `TextEditor`
- `HorizontalScrollBarVisibility="Hidden"` and `VerticalScrollBarVisibility="Hidden"` for single-line inputs
- Font propagated via `ApplyEditorFont` from app-level `UiFontFamily`/`UiFontSize` bindings

Standard `TextBox` controls inherit from the Fluent theme automatically — no extra styling needed. Never mix raw `TextEditor` and styled `TextBox` in the same row without ensuring equal effective height and padding.

## 21. VM-Based System Tests `[RECOMMENDED][PROCESS]`

For UI changes (`.axaml`, ViewModels, main window layout), run VM-based system tests before declaring a PR ready. See [`docs/vm-ui-automation.md`](docs/vm-ui-automation.md) for setup and usage for both Hyper-V (Windows) and KVM+Alpine (Linux/CI). Optional for backend-only changes.

## 22. Test Command Timeout

**Always run test commands with a hard timeout (maximum 120 seconds).**
