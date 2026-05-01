# Copilot Instructions

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 0. Read All Markdown Files First

**At the start of every session, read all Markdown files in the repository before making any decisions or changes.**

The repository contains documentation that shapes how every task should be approached:

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

Reading these files ensures you apply the correct standards, avoid known pitfalls, and build on prior decisions rather than re-litigating them.

### How agents consume these instructions

Different agent surfaces load these instructions differently. The table below describes how each surface picks them up and what to do when a surface does not load them automatically.

| Agent / Surface | How instructions are loaded | What to do if not auto-loaded |
|---|---|---|
| **GitHub Copilot coding agent** (cloud) | Loads `.github/copilot-instructions.md` automatically as system context | Nothing — automatic |
| **Copilot Chat in VS Code** | Loads `.github/copilot-instructions.md` via `.vscode/settings.json` `codeGeneration.instructions` | Open the repository folder; settings take effect automatically |
| **Copilot Chat (review selection)** | Loads `.github/copilot-instructions.md` + `docs/review-checklist.md` via `.vscode/settings.json` `reviewSelection.instructions` | Same as above |
| **Copilot Chat (commit messages)** | Loads commit message style via `.vscode/settings.json` `commitMessageGeneration.instructions` | Same as above — imperative mood, ≤ 72 chars, issue reference when applicable |
| **Claude coding agent** (Anthropic) | Loads `CLAUDE.md` automatically at session start | Nothing — automatic; `CLAUDE.md` references the canonical `.github/copilot-instructions.md` |
| **Codex agent** (OpenAI) | Loads `AGENTS.md` automatically at session start | Nothing — automatic; `AGENTS.md` references the canonical `.github/copilot-instructions.md` |
| **Reusable prompt files** | `.github/prompts/*.prompt.md` — reference with `#<filename>.prompt.md` in Copilot Chat | Type `#pr-checklist.prompt.md` etc. in the chat input |
| **Third-party agents** (Cursor, Aider, etc.) | Do not auto-load; must be explicitly referenced | Start your session with: `Read and follow .github/copilot-instructions.md` — or use the `#code-standards.prompt.md` shorthand |

### Reusable prompt files

The `.github/prompts/` directory contains focused, self-contained prompts for specific tasks. Each file can be used standalone (it contains the key rules) or as a shortcut that points to the full instructions.

| Prompt file | When to use |
|---|---|
| `code-standards.prompt.md` | Start of any coding session with an agent that does not auto-load this file |
| `pr-checklist.prompt.md` | End of every PR — generates the compliance checklist for the PR description |
| `ux-review.prompt.md` | Any PR — guides the UX ideas maintenance task (section 13) |

### Scoped instruction files

The `.github/instructions/` directory contains per-file-type instruction files that load automatically in supported editors (VS Code with GitHub Copilot extension). They extract the most relevant rules for each file type so agents only receive the rules that apply to the file being edited, reducing token cost.

| Instruction file | Applies to | Key rules |
|---|---|---|
| `csharp.instructions.md` | `**/*.cs` | Async/CancellationToken, exception handling, test naming, `is null`/`is { }`, `nameof`, `readonly`, no AAA comments, DateTimeOffset, file-scoped namespaces, nullable global-only |
| `avalonia.instructions.md` | `**/*.axaml` | Fluent theme metrics, TextEditor styling, WCAG contrast, accessibility |
| `github-actions.instructions.md` | `.github/workflows/*.yml` | DRY principle (ci.yml ↔ release.yml), persist-credentials, vulnerability audit command, sbom-tool convention |

These files are supplements, not replacements. The full authoritative rules remain in this file.

## Repository Constants

The following values are used throughout instructions and workflows. They are listed here once so agents do not have to re-derive them from context.

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
- If multiple interpretations exist, present them - don't pick silently.
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

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

## 5. All Tests Must Pass Before Committing

**Never commit code that breaks the test suite.**

- Run `dotnet test Arbor.HttpClient.slnx` and confirm it exits with no failures before every commit.
- **Exception**: Analysis-only, planning, and informational requests that produce no file changes are exempt from this check.
- A pre-commit Git hook is available to automate this check. Set it up once after cloning:
  ```
  ./scripts/install-hooks.sh
  ```
- `git commit --no-verify` bypasses the hook. Use it only in genuinely exceptional circumstances (e.g. committing a work-in-progress branch where tests are intentionally broken and you will fix them in the next commit). Never push to `main` with failing tests.
- If a pre-existing test was already failing before your changes, note it explicitly in the PR description rather than silently ignoring it.

### CI Parity — Run the Same Checks Locally Before Committing

**The goal is to catch issues before they reach GitHub Actions.** CI runs require user consent, which adds an extra feedback loop. Running equivalent checks locally before committing short-circuits that delay and surfaces problems immediately.

The following local commands mirror the CI jobs in `.github/workflows/ci.yml`. Run them in order before every commit:

| CI job | Local equivalent |
|--------|-----------------|
| **Restore** | `dotnet restore Arbor.HttpClient.slnx --locked-mode` |
| **Vulnerability audit** | `dotnet list Arbor.HttpClient.slnx package --vulnerable --include-transitive` |
| **Build** | `dotnet build Arbor.HttpClient.slnx --no-restore --configuration Release` |
| **Unit tests** | `dotnet test src/Arbor.HttpClient.Core.Tests/Arbor.HttpClient.Core.Tests.csproj --no-restore --configuration Release` |
| **E2E tests** | `dotnet test src/Arbor.HttpClient.Desktop.E2E.Tests/Arbor.HttpClient.Desktop.E2E.Tests.csproj --no-restore --configuration Release` |
| **UX screenshots** | `./scripts/take-screenshots.sh` *(UI changes only — commits to `docs/screenshots/`)* |
| **Agent instructions sync** | Bash: `diff <(tail -n +2 CLAUDE.md) <(tail -n +2 AGENTS.md)` · PowerShell: `Compare-Object (Get-Content CLAUDE.md \| Select-Object -Skip 1) (Get-Content AGENTS.md \| Select-Object -Skip 1)` |

If any step fails locally, fix it before pushing — this avoids triggering a CI run only to discover a preventable failure.

## 6. Runtime Validation Before PR Ready

**Run the application headlessly and fix every console error before the PR is ready.**

- After completing feature or bug-fix work on the desktop application, run it headlessly using the Avalonia headless test infrastructure (`HeadlessUnitTestSession`) or via `dotnet run` with output captured.
- Capture the full standard output and standard error of the run.
- Treat any `[Binding]` error, `[Control]` error, or unhandled exception in the output as a defect that must be fixed — not ignored.
- Exceptions: Dock framework transient null-binding noise emitted by the Dock library's own XAML templates (e.g. `DockCapabilityOverrides.CanClose` / `Owner.DockCapabilityPolicy.CanClose` during panel transitions) cannot be fixed without patching the upstream library; document these explicitly in the PR rather than silently ignoring them.
- Re-run after every fix and repeat until the output is clean of actionable errors.
- Include the captured (clean) console output as evidence in the PR description or checklist.

## 7. Code Quality Requirements for New Work

- Treat compiler warnings, analyzer warnings, and runtime errors as real defects. Do not ignore or suppress them unless there is a documented and justified reason.
- **Analyzer severity policy**: Promote correctness and security-sensitive Roslyn/CA rules to `warning` (or `error`). Keep style-only rules as `suggestion` to avoid noisy CI failures. Never silently suppress an analyzer diagnostic — add a code comment with the justification when suppression is truly necessary.
- **Code coverage requirements**:
  - Any new or changed production code must include test coverage
  - Prefer isolated unit tests first, then integration/E2E tests when unit tests are not sufficient
  - Maintain reasonably high coverage in the changed area. If code can be tested, add tests
  - For feature work, generate coverage reports locally and review them before committing
  - Current coverage baseline: see [`docs/coverage.md`](docs/coverage.md) — **that file is the single source of truth for coverage numbers; never write inline percentages here or in the PR description without reading the XML output yourself**
  - New code should not lower the overall coverage percentage
  - CI automatically generates and publishes coverage reports to the job summary
  - **[REQUIRED]** Coverage numbers reported in `docs/coverage.md` and the PR description must be sourced from an actual `dotnet test --collect:"XPlat Code Coverage"` run, not estimated. Never write coverage percentages unless you have collected and read the coverage XML output yourself.
  - **[REQUIRED]** `docs/coverage.md` is the single source of truth for coverage numbers. Do not repeat coverage percentages in `copilot-instructions.md`, `README.md`, or other files — link to `docs/coverage.md` instead.
  - **Coverage targets for new code** — enforced per PR:
    - `Arbor.HttpClient.Core` (pure logic, no UI): **100% line coverage** for new or changed classes. If a code path genuinely cannot be exercised without real network connectivity (e.g. `WebSocketService.ConnectAsync` requires a live server), document the gap explicitly in the PR and cover all testable branches (validation, error paths, disposal).
    - `Arbor.HttpClient.Desktop` (UI/integration): **90% line coverage** for new or changed ViewModels and services. UI-thread dispatch paths and Avalonia lifecycle hooks that cannot run in the headless test harness are exempt; document any such exemption.
    - `Arbor.HttpClient.Storage.Sqlite`: **90% line coverage** for new or changed repositories.
    - `Arbor.HttpClient.Testing`: no minimum — this project provides test doubles and its coverage is driven indirectly.
  - After every feature PR, run coverage, read the XML output, update `docs/coverage.md`, and include the numbers in the PR description.
- **Test naming convention**: Name tests using the `Method_Scenario_ExpectedResult` pattern (e.g. `Parse_EmptyInput_ThrowsArgumentException`). Each test should verify one behavioral intent; arrange test data explicitly rather than relying on implicit state.
- **[REQUIRED][QUALITY]** Integration tests — tests that exercise real external resources (file system, network, process environment variables, database, etc.) — must be clearly distinguished from pure unit tests:
  - Annotate integration test classes with `[Trait("Category", "Integration")]`.
  - Any tests that mutate process-global state (e.g. `Environment.SetEnvironmentVariable`) must belong to a dedicated xUnit collection with `DisableParallelization = true` so they do not race with other tests that read the same state. Define the collection in a `*Collection.cs` file in the same test project:
    ```csharp
    [CollectionDefinition("ProcessEnvironment", DisableParallelization = true)]
    public sealed class ProcessEnvironmentCollection;
    ```
    Then apply the collection to every test class that mutates that state:
    ```csharp
    [Collection("ProcessEnvironment")]
    [Trait("Category", "Integration")]
    public class MyIntegrationTests { ... }
    ```
  - Always save the previous value of any mutated global state before the test and restore it in a `finally` block (or equivalent teardown), even if the variable was `null` before the test. This ensures cleanup is complete whether the test passes or fails and avoids leaking state changes that could affect the rest of the test run.
- Profiling-oriented validation is required when changing request execution hot paths, scheduled/background job loops, data-processing loops, or code that introduces disposable/resource-heavy objects.
- Treat code as a hot path when it runs on every request, for each item in a collection, or on a recurring timer. Profiling is optional for isolated admin/one-off flows.
- Use JetBrains dotMemory Unit or equivalent tools (for example `dotnet-counters` or BenchmarkDotNet) to catch memory leaks, performance bottlenecks, or resource leaks. Attach profiling evidence in the PR when this requirement applies.
- For UI-related changes, **screenshots must appear inline in the PR description** so reviewers can see them without downloading anything. Screenshots saved only to `/tmp/` or other ephemeral paths are inaccessible to reviewers and do not satisfy this requirement.
  - **Correct workflow for every UI change:**
    1. Run the screenshot script (which builds and runs the `Category=Screenshots` E2E tests):
       ```bash
       ./scripts/take-screenshots.sh
       ```
       Or invoke the tests directly to target a specific output directory:
       ```bash
       SCREENSHOT_OUTPUT_DIR=docs/screenshots dotnet test \
         src/Arbor.HttpClient.Desktop.E2E.Tests/Arbor.HttpClient.Desktop.E2E.Tests.csproj \
         --configuration Release \
         --filter "Category=Screenshots"
       ```
    2. Commit the generated `docs/screenshots/*.png` files via `report_progress`.
    3. In the `prDescription` passed to `report_progress`, embed each relevant screenshot using a relative markdown image path:
       ```markdown
       ![Main window after change](docs/screenshots/main-window.png)
       ```
       GitHub renders relative repository paths in PR descriptions inline — the image appears immediately without any download step.
  - Do **not** save screenshots only to `/tmp/` or to any path outside the repository tree — those files disappear and reviewers cannot see them.

## 8. Public API Change Policy

- Document and review public API changes, especially in `Arbor.HttpClient.Core`.
- Surface breaking changes (removed members, signature changes, changed semantics) explicitly in the PR description.
- Optionally introduce API baseline tooling if package or public API stability becomes important in the future.

## 9. Async and Cancellation Conventions

- Every async method that performs I/O or can block must accept a `CancellationToken` parameter.
- Pass the token through to all downstream async calls unless there is a clear, documented reason not to (e.g. a fire-and-forget cleanup path).
- Name the parameter `cancellationToken` (not `ct` or other abbreviations) for consistency with the .NET BCL.

## 9a. Date and Time Parsing Conventions

- **[REQUIRED][QUALITY]** Always pass `CultureInfo.InvariantCulture` (never `null`) to `DateTimeOffset.TryParse`, `DateTimeOffset.Parse`, `DateTime.TryParse`, and similar parsing overloads that accept a format provider. Passing `null` silently falls back to the current thread culture and will produce incorrect results or silent parse failures on non-English systems.
  ```csharp
  // ✅ culture-invariant — safe on all locales
  DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)

  // ❌ uses current thread culture — silently breaks on non-English locales
  DateTimeOffset.TryParse(value, null, DateTimeStyles.RoundtripKind, out var parsed)
  ```
- **[REQUIRED][QUALITY]** When reading a stored UTC timestamp (e.g. from SQLite `TEXT` column), call `.ToUniversalTime()` after parsing to normalise the offset to UTC regardless of what offset the stored string carries.

## 9b. LINQ Conventions

- **[REQUIRED][QUALITY]** Use `.Any(predicate)` instead of a `foreach + if + return true / return false` existence-check pattern. The loop form triggers the CodeQL "Missed opportunity to use Where" diagnostic. The `.Any()` form is also more readable:
  ```csharp
  // ✅ idiomatic LINQ
  return keywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));

  // ❌ verbose loop — triggers CodeQL
  foreach (var k in keywords) { if (condition) return true; } return false;
  ```
- **[REQUIRED][QUALITY]** Use `.Where(predicate)` instead of an inner `if` inside a `foreach` when filtering a collection. This prevents the same CodeQL diagnostic.
- **[REQUIRED][QUALITY]** Combine nested `if` statements into a single compound condition when there is no intermediate logic between them. Nested `if`s without intervening code trigger the CodeQL "Nested 'if' statements can be combined" diagnostic:
  ```csharp
  // ✅ combined
  if (condition1 && condition2) { ... }

  // ❌ triggers CodeQL
  if (condition1)
  {
      if (condition2) { ... }
  }
  ```

## 10. Logging and Observability Conventions

- Use structured logging fields consistently: `requestId`, `environment`, `jobId`, `statusCode`, `durationMs`.
- Severity guidance:
  - `Information` — routine, expected events (job invoked, request sent).
  - `Warning` — unexpected but recoverable situations (retry attempt, fallback used).
  - `Error` — failures that require attention (connection refused, unhandled exception).
- Do not log sensitive data (credentials, PII, raw request bodies that may contain secrets).

## 11. Exception-Handling Conventions

- Prefer domain-specific exception types (or result/discriminated-union types) at subsystem boundaries instead of leaking generic `Exception`.
- When rethrowing, use bare `throw;` to preserve the original stack trace — never `throw ex;`.
- Catch only exceptions you can handle meaningfully; let the rest propagate to a top-level handler.
- **[REQUIRED][QUALITY]** Never use a bare `catch {}` clause (catches unmanaged/SEH exceptions) or `catch (Exception) {}` without a `when` filter. Always specify the concrete exception type(s) you expect, using a `when` predicate when multiple unrelated types must be caught in one clause:
  ```csharp
  // ✅ specific types via when predicate
  catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
  {
      // silently ignore: no default browser or invalid URL
  }

  // ❌ too broad — triggers CodeQL cs/catch-of-all-exceptions
  catch { }
  catch (Exception) { }
  ```
- **[REQUIRED][QUALITY]** Remove `try/catch` wrappers entirely when the enclosed code cannot realistically throw (e.g. generated `ObservableProperty` setters, trivial property reads). Defensive catches that swallow real bugs are worse than no catch at all.

## 12. Path-Handling Conventions

- **[REQUIRED][QUALITY]** Use `Path.Join` instead of `Path.Combine` whenever any argument is not a compile-time string literal. `Path.Combine` silently discards its earlier arguments when a later argument is rooted (starts with `/` or a drive letter), which is a CodeQL-flagged footgun (`cs/path-combine-user-controlled`). `Path.Join` never silently drops arguments.
  ```csharp
  // ✅ safe — Join never drops earlier segments
  var dbPath = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");

  // ❌ may silently discard Path.GetTempPath() if the GUID somehow starts with /
  var dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
  ```
  The only acceptable use of `Path.Combine` is when **all** arguments are compile-time string literals (e.g. `Path.Combine("docs", "screenshots", "main.png")`), where the silent-drop behaviour is impossible.

## 13. Structured Instruction Format

**Instructions benefit from machine-readable attributes, not just prose.**

Free-text guidelines are easy to write but hard to enforce consistently. Where possible, annotate instructions with structured markers that make severity and category explicit — both to you (the agent) and to future contributors reading or extending this file.

### Severity attributes

Prefix each instruction or checklist item with one of the following tags so that priority is unambiguous:

| Tag | Meaning |
|-----|---------|
| `[BLOCKING]` | Must be satisfied before a PR can be merged. Failure is a hard stop. |
| `[REQUIRED]` | Must be satisfied in normal circumstances. Skip only with an explicit documented justification. |
| `[RECOMMENDED]` | Should be satisfied. Deviations are acceptable with a brief note explaining why. |
| `[OPTIONAL]` | Nice to have. No justification needed if omitted. |

### Category attributes

Tag each instruction with one or more of the following categories so tooling or future automation can filter by concern:

| Tag | Concern |
|-----|---------|
| `[SECURITY]` | Security posture, credentials, TLS, injection risk |
| `[QUALITY]` | Test coverage, compiler warnings, analyzer findings |
| `[PROCESS]` | Workflow steps, PR lifecycle, documentation updates |
| `[ACCESSIBILITY]` | WCAG compliance, keyboard navigation, screen-reader labels |
| `[ARCHITECTURE]` | Separation of concerns, dependency direction, testability |
| `[PERFORMANCE]` | Hot paths, memory leaks, profiling evidence |

### How to apply

When adding a new instruction to this file, format it as:

```
**[BLOCKING][SECURITY]** No secrets, tokens, or credentials may be committed.
**[REQUIRED][QUALITY]** All tests must pass before committing (`dotnet test Arbor.HttpClient.slnx`).
**[RECOMMENDED][PROCESS]** Screenshot evidence committed to `docs/screenshots/` for UI changes.
```

Existing prose sections do not need to be retroactively reformatted all at once; apply the tags incrementally when a section is edited. This creates a gradual migration path rather than a big-bang rewrite.

### Attribute-based enforcement loop

At the end of every PR, scan the instructions for `[BLOCKING]` items and verify each one explicitly in the compliance checklist (see section 16). For `[REQUIRED]` items, either confirm compliance or record a justification. `[RECOMMENDED]` and `[OPTIONAL]` items can be marked as skipped without further explanation.

## 14. PR Task: UX Ideas Maintenance `[REQUIRED][PROCESS]`

**Every PR must keep `docs/ux-ideas.md` current.**

`docs/ux-ideas.md` is the product backlog for UX improvements. It must always reflect two clearly separated lists so that contributors know what is available to work on and what has already shipped.

### What to do on every PR

1. Open `docs/ux-ideas.md` and read every idea.
2. For each idea that was **fully or partially implemented** by this PR, move its entry from the "Not Yet Implemented" section to the "Implemented" section and add a reference:
   - PR number (e.g. `#42`)
   - Commit SHA (short form, 7 characters)
   - File or component where the feature lives (e.g. `src/Arbor.HttpClient.Desktop/ViewModels/MainWindowViewModel.cs`)
3. If this PR introduces a **new UX idea** (discovered during implementation or review), add it to the "Not Yet Implemented" section with the standard description and scope estimate.
4. Do **not** delete ideas from either list; the "Implemented" section is a historical record.

### Format for implemented entries

```markdown
### 1.1 Feature Name ✅ Implemented
> Implemented in PR #42 (commit `a1b2c3d`) — `src/path/to/Feature.cs`

**What it means:** (original description retained)
...
```

### What counts as "implemented"

An idea is considered implemented when its primary UX behaviour is usable in the application, even if polish items remain. Record those polish gaps as sub-items in the implemented entry rather than keeping the whole idea in "Not Yet Implemented".

## 15. PR Task: Instruction Improvement Loop `[RECOMMENDED][PROCESS]`

**After every PR, propose at least one instruction improvement.**

Each PR is a learning event. When a task is complete, reflect on what was done, which instructions guided the work, ambiguities encountered, and how future user-agent interactions could be improved. Propose concrete changes to this instructions file or the other docs that would reduce friction in future sessions.

### Workflow

1. At the end of the PR (after code is ready and tests pass), write a short "Instruction Retrospective" block in the PR description:

   ```markdown
   ## Instruction Retrospective

   ### What was done
   [Summarise the work completed in this PR — features added, bugs fixed, docs updated.]

   ### Instructions consulted
   [List which sections of `.github/copilot-instructions.md` (and any other doc files) were actively
   applied during the session — e.g. "§5 CI Parity, §7 Code Quality, §15 Compliance Checklist".]

   ### Ambiguities encountered
   - **In instructions:** [Describe any guideline that was unclear, missing, or contradictory.]
   - **In user requests:** [Describe any part of the problem statement that required interpretation
     or clarification — and what assumption was made.]

   ### What caused rework
   [Describe any decision that had to be revisited after initial implementation.]

   ### Proposed improvement
   [Draft wording for a new or updated instruction, including severity/category tags.
   If nothing needs changing, write "None — instructions were clear and complete."]

   ### User-agent interaction ideas
   [Suggest anything that would make future task handoffs smoother — e.g. prompts that reduce
   ambiguity, checklist items that should become blocking, workflow steps to automate.]
   ```

2. If the proposed addition is clearly beneficial and self-contained, apply it directly to `.github/copilot-instructions.md` (or the relevant doc) as part of the same PR. This is the improvement loop.

3. If the proposed addition requires discussion (e.g. it changes a `[BLOCKING]` rule or touches architectural decisions), add it as a GitHub issue and link it from the retrospective block instead of applying it immediately.

### What to look for

- Instructions that said "do X" but the codebase made X impossible without extra steps not mentioned.
- Instructions that were silent on a decision that had to be made repeatedly.
- Checklists that were hard to verify because the criterion was vague.
- Instructions whose scope or category tag was missing and caused uncertainty about priority.
- User requests that were ambiguous — and what additional context upfront would have resolved the ambiguity immediately.
- Patterns in user-agent back-and-forth that could be eliminated with a clearer prompt template or a pre-flight question.

## 16. End-of-PR Compliance Checklist `[BLOCKING][PROCESS]`

**Before marking any PR ready for review, verify every blocking principle.**

At the end of every PR session, re-read all markdown files listed in section 0 and complete the following checklist. Include the completed checklist (with actual ✅ / ❌ / N/A values, not blank boxes) in the PR description.

> **[BLOCKING]** The completed checklist **must** appear in the `prDescription` field of the **final** `report_progress` call that closes out the session. A `report_progress` call without the filled-in checklist in `prDescription` is non-compliant. Do not end the session until this step is complete.

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
- [ ] **[REQUIRED]** docs/ux-ideas.md reviewed; implemented ideas moved to the "Implemented" section with PR/commit references
- [ ] **[RECOMMENDED]** New UX ideas discovered during this PR added to the "Not Yet Implemented" section

### From docs/architecture/clean-feature-separation.md
- [ ] **[RECOMMENDED]** No new logic added to MainWindowViewModel that belongs in a feature VM
- [ ] **[RECOMMENDED]** New features have at least one focused unit test not requiring the full UI runtime
- [ ] **[REQUIRED]** Test project boundaries respected: tests for a library reference only that library (plus Arbor.HttpClient.Testing); no cross-layer project references added to existing test projects

### From section 17 (License Compatibility)
- [ ] **[REQUIRED]** New NuGet packages have a license in the compatible list (MIT, Apache-2.0, BSD, ISC, OFL-1.1) *(skip if no new packages)*
- [ ] **[REQUIRED]** New packages declared in Directory.Packages.props, not inline in .csproj *(skip if no new packages)*

### From section 18 (MSIX Packaging and Releases)
- [ ] **[REQUIRED]** release.yml changes mirrored in the release-verification job in ci.yml *(skip if no workflow changes)*
- [ ] **[REQUIRED]** Shared workflow logic extracted to scripts/ — not duplicated inline *(skip if no workflow changes)*

### From section 19 (Accessibility)
- [ ] **[REQUIRED]** New color pairs in App.axaml covered by AccessibilityContrastTests.cs *(skip if no theme color changes)*
- [ ] **[REQUIRED]** Interactive controls keyboard-accessible *(skip if no UI changes)*

### Instruction Improvement Loop (section 14)
- [ ] **[RECOMMENDED]** Instruction Retrospective block written in PR description (work done, instructions consulted, ambiguities, user-agent interaction ideas)
- [ ] **[RECOMMENDED]** Proposed instruction improvement applied (or tracked as a GitHub issue)

### Final self-check
- [ ] **[BLOCKING]** Every changed line traces directly to the user's request (no unrelated edits)
- [ ] **[REQUIRED]** PR description explains what changed and why
```

---

*Behavioral guidelines adapted from [vlad-ko/claude-wizard](https://github.com/vlad-ko/claude-wizard), used under the [MIT License](https://github.com/vlad-ko/claude-wizard/blob/main/LICENSE) (Copyright 2026 Vlad Ko).*

<a id="license-compatibility"></a>
## 17. License Compatibility

This project is licensed under the **MIT License**. When adding new NuGet packages or any other third-party dependencies, you **must** verify that their licenses are compatible with MIT before including them.

### Compatible licenses (permitted)

The following license families are compatible with MIT and may be used freely:

- **MIT** — fully compatible
- **Apache-2.0** — compatible; requires attribution and inclusion of the Apache license notice when distributing
- **BSD-2-Clause / BSD-3-Clause** — compatible
- **ISC** — compatible
- **SIL Open Font License 1.1 (OFL-1.1)** — compatible for fonts embedded in software

### Incompatible or restricted licenses (requires review before use)

Do **not** add packages under these licenses without explicit approval:

- **GPL-2.0 / GPL-3.0** — copyleft; incompatible with MIT distribution
- **LGPL-2.1 / LGPL-3.0** — conditionally compatible only; requires careful evaluation
- **AGPL-3.0** — network copyleft; incompatible
- **SSPL** — incompatible
- **Proprietary / Commercial** — requires explicit legal approval
- **No license specified** — treat as "all rights reserved"; requires explicit approval

### Steps when adding a new NuGet package

1. Check the package's license on [NuGet.org](https://www.nuget.org) (the "License" field) or its GitHub/source repository.
2. Confirm the license is in the **Compatible** list above.
3. Add an entry to **`THIRD_PARTY_NOTICES.md`** in the repository root with:
   - Package name and version
   - Authors and copyright statement
   - Project URL
   - License identifier (SPDX expression preferred, e.g. `MIT`, `Apache-2.0`)
   - A note if the dependency is test-only or debug-only (not redistributed)
4. Update the package version in `Directory.Packages.props` (this project uses [Central Package Management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)).
5. Do **not** add a `Version` attribute directly in any `.csproj` file.

### Dependency governance

- Keep dependencies up to date on a regular cadence (at least monthly review via Dependabot alerts).
- Triage CVEs within 7 days of disclosure for direct dependencies, 30 days for transitive dependencies.
- Prefer dependencies with active maintenance and a compatible open-source license.
- Avoid adding dependencies that duplicate functionality already available in the BCL or an existing project dependency.

### Example THIRD_PARTY_NOTICES.md entry

```markdown
## ExamplePackage

**Package:** ExamplePackage
**Version:** 1.2.3
**Authors:** Example Author
**Copyright:** Copyright © 2024 Example Author
**Project URL:** https://github.com/example/example
**License:** MIT
```

## 18. MSIX Packaging and Releases

The desktop application (`Arbor.HttpClient.Desktop`) is distributed as a signed MSIX package.

### How it works

- The MSIX manifest template lives at `src/Arbor.HttpClient.Desktop/packaging/AppxManifest.xml`.
- `VERSION_PLACEHOLDER` in the manifest is substituted at build time with the 4-part version derived from the GitHub Actions run number (`1.0.{run_number}.0`).
- The release workflow (`.github/workflows/release.yml`) runs automatically on every push to `main` and can also be triggered manually via `workflow_dispatch`. It:
  1. Builds and runs unit tests on `windows-latest`.
  2. Publishes a `win-x64` self-contained executable via `dotnet publish`.
  3. Generates required MSIX logo assets using ImageMagick.
  4. Packages the publish output with `makeappx.exe` (Windows SDK, pre-installed on the runner).
  5. Creates a self-signed certificate and signs the MSIX with `signtool.exe`.
  6. Creates a GitHub Release with auto-generated release notes and uploads the MSIX and the `.cer` sideloading certificate as assets.

### When modifying the desktop app

- Keep the `AppxManifest.xml` consistent with the app identity (`NiklasLundberg.ArborHttpClient`, Publisher `CN=Arbor.HttpClient`).
- Do **not** change `ProcessorArchitecture` in the manifest without also changing the `-r` runtime identifier in the release workflow's `dotnet publish` step.
- The `Publisher` value in `AppxManifest.xml` must exactly match the `Subject` of the signing certificate. If you change the publisher, update both.
- Required MSIX logo sizes: `Square44x44Logo` (44×44), `Square150x150Logo` (150×150), `Wide310x150Logo` (310×150), `StoreLogo` (50×50), `SplashScreen` (620×300). Update the workflow if real brand assets replace the generated placeholders.
- **`[RECOMMENDED][PROCESS]`** Release workflows must include a `workflow_dispatch` trigger and handle existing releases/tags idempotently (e.g. delete before re-creating) so that they can be re-triggered without pushing a new commit to `main`.
- **`[REQUIRED][PROCESS]`** The CI workflow (`ci.yml`) must contain a `release-verification` job that mirrors every step of `release.yml` **except** `Attest build provenance` (requires `id-token: write`) and `Create GitHub Release`. This ensures packaging failures (MSIX layout, SBOM generation, signing) are caught on PRs before reaching `main`. When modifying `release.yml`, always apply the same change to the corresponding step in the `release-verification` job in `ci.yml`.
- **`[REQUIRED][PROCESS]`** Build and packaging logic must never be duplicated across workflows or scripts. Extract any shell or PowerShell steps that are shared between `release.yml` and `ci.yml` into a reusable script under `scripts/` (e.g. `scripts/Build-Release.ps1`) and invoke that script from both workflows. The general rule is: a "thing" should exist in exactly one place. The only permitted exception is test code, where isolation and decoupling are more important than deduplication.
- **`[RECOMMENDED][PROCESS]`** The `sbom-tool` always appends `_manifest` to the directory passed via `-m`. Pass `-m <build-drop-path>` (e.g. `-m publish/win-x64`) so the manifest is written to `<build-drop-path>/_manifest/spdx_2.2/manifest.spdx.json`. Never pass `-m <build-drop-path>/_manifest` or the tool will double-nest the directory.

## 19. Accessibility

All UI changes involving human interaction must consider accessibility from the start — not as an afterthought.

### Requirements

- **Color contrast**: Every foreground/background color pair used for text or interactive elements must meet [WCAG 2.1](https://www.w3.org/WAI/standards-guidelines/wcag/) Level AA:
  - ≥ 4.5:1 for normal text
  - ≥ 3:1 for large text (bold text ≥ 14 pt, or regular text ≥ 18 pt) and graphical/UI components
- **Theme consistency**: Colors must be defined per-theme (Dark/Light) in the `ResourceDictionary.ThemeDictionaries` section of `App.axaml` so that each variant meets the above ratios against its own backgrounds.
- **Contrast tests**: Any new color pair introduced in `App.axaml` must be covered by a corresponding test case in `AccessibilityContrastTests.cs` that asserts the WCAG contrast ratio.
- **Keyboard navigation**: Interactive controls (buttons, list items, text boxes) must be reachable and operable by keyboard alone.
- **Screen reader labels**: All non-decorative icons and images must carry an accessible name (e.g., `AutomationProperties.Name`).

### Verification checklist for UI pull requests

Before merging any PR that touches UI code or theme resources:

- [ ] All new/changed color pairs have been verified with the contrast-ratio formula in `AccessibilityContrastTests.cs` and meet WCAG AA.
- [ ] Interactive elements remain keyboard-accessible (Tab, Enter/Space, arrow keys where applicable).
- [ ] No purely visual text label has been replaced by an icon without an accessible name.
- [ ] New interactive controls have been manually verified with keyboard-only navigation.

## 20. UI Consistency

All text-input controls must have a consistent look and feel that matches the Avalonia Fluent theme.

- **Text inputs that replace a standard `TextBox`** (e.g. `AvaloniaEdit.TextEditor` used for variable highlighting in the URL bar) must match the Fluent `TextBox` visual metrics:
  - `Padding="5,6,5,6"` to align text at the same vertical position as a `TextBox` (effective centering for the default 13 px font in a 32 px row)
  - `CornerRadius="3"` on the surrounding `Border` (Fluent standard)
  - `Background` on the surrounding `Border` set to `{DynamicResource SurfaceBackgroundBrush}` so the background adapts with the active theme
  - The inner `TextEditor` itself must be `Background="Transparent"` so the Border background is visible
  - Scrollbars hidden (`HorizontalScrollBarVisibility="Hidden"` and `VerticalScrollBarVisibility="Hidden"`) for single-line inputs
  - Font family and size must be explicitly propagated from the app-level `UiFontFamily`/`UiFontSize` bindings via `ApplyEditorFont`
- **`TextBox` controls that remain as plain `TextBox`** do not need additional styling; they automatically inherit font and appearance from the Fluent theme and window-level `FontFamily`/`FontSize` bindings.
- Never mix raw `TextEditor` controls and styled `TextBox` controls in the same row or form group without ensuring they have the same effective height and padding.

## 21. VM-Based System Tests (Pre-Commit Check)

### 21a. Hyper-V (Windows developers)

**`[RECOMMENDED][PROCESS]`** When working on the desktop application (`Arbor.HttpClient.Desktop`) on a Windows 11 machine with Hyper-V enabled, run the VM-based system tests before declaring a PR ready.

#### Quick availability probe

Run `scripts/Test-HyperVAvailability.ps1` first. This is a fast, non-destructive check (no VMs created). Exit code 0 means Hyper-V is available and you can proceed with full system tests.

```powershell
scripts/Test-HyperVAvailability.ps1
if ($LASTEXITCODE -eq 0) {
    Write-Host "Hyper-V is available — full system tests can run."
} else {
    Write-Host "Hyper-V not available on this machine — skip full system tests."
}
```

If exit code is 0, run the full system tests:

```powershell
scripts/Start-UIAutomation.ps1 `
    -BaseVhdx  "C:\HyperV\Base\win11-base.vhdx" `
    -OutputDir "docs\system-test-screenshots\light" `
    -RecordVideo `
    -Theme Light       # preferred for video recordings
```

For dark-theme screenshots, run a second pass:

```powershell
scripts/Start-UIAutomation.ps1 `
    -BaseVhdx  "C:\HyperV\Base\win11-base.vhdx" `
    -OutputDir "docs\system-test-screenshots\dark" `
    -Theme     Dark
```

#### CI integration (Hyper-V)

- Full VM system tests run on demand via `.github/workflows/system-tests.yml` (`workflow_dispatch`).
- The Hyper-V environment probe runs on demand via `.github/workflows/vm-probe.yml` (`workflow_dispatch`).
- Neither workflow runs automatically in the standard CI pipeline — they are on-demand only.

#### Base VHDX preparation

See `docs/vm-ui-automation.md` section 4 for step-by-step instructions on preparing a sysprepped Windows VHDX to use as the base image.

---

### 21b. KVM + Alpine Linux (Linux / cross-platform developers)

**`[RECOMMENDED][PROCESS]`** On any Ubuntu 22.04+ machine (or in CI with a large Linux runner), use the KVM+Alpine approach. The base image is downloaded automatically — no manual image preparation required.

#### Prerequisites

```bash
sudo apt-get install -y \
    qemu-kvm qemu-utils cloud-image-utils \
    sshpass ffmpeg dotnet-sdk-10
```

#### Run system tests

```bash
# Basic run (auto-downloads Alpine 3.21.7 image on first run)
./scripts/start-ui-automation-kvm-alpine.sh

# With video recording
./scripts/start-ui-automation-kvm-alpine.sh --record-video

# With step-by-step inspection (connect via VNC viewer at localhost:5911)
./scripts/start-ui-automation-kvm-alpine.sh --record-video --pause

# Reuse a cached image (faster subsequent runs)
./scripts/start-ui-automation-kvm-alpine.sh \
    --base-image /tmp/arbor-vms/nocloud_alpine-3.21.7-x86_64-bios-cloudinit-r0.qcow2
```

Artifacts are written to `docs/system-test-screenshots/alpine/` by default:
- `step-01-*.png` … `step-06-*.png` — UI automation screenshots
- `alpine-test-results.json` — structured step outcomes and timing
- `alpine-test-report.md` — human-readable Markdown summary
- `demo-alpine.mp4` (when `--record-video` is used)

#### CI integration (KVM + Alpine)

- On-demand tests run via `.github/workflows/kvm-alpine-tests.yml` (`workflow_dispatch`).
- Artifacts are uploaded to the GitHub Actions run for every invocation.
- Use `commit_artifacts: true` to commit screenshots to `docs/system-test-screenshots/alpine/`.

#### When to run

- **Always** when making changes to the application UI (`.axaml`, ViewModels, main window layout).
- **Optional** for backend-only changes (services, HTTP logic, storage).
