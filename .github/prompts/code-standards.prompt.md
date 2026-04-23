---
mode: agent
description: Core coding standards quick-reference — apply these on every code change in this repository.
---

# Coding Standards Quick Reference

> Canonical source: `.github/copilot-instructions.md`. This prompt is a self-contained summary for agents that do not automatically load that file. When in doubt, defer to the canonical source.

## Must-read files at session start

Read these files before making any decision or change:

| File | Why |
|------|-----|
| `.github/copilot-instructions.md` | Full behavioral guidelines (canonical) |
| `docs/review-checklist.md` | PR review items (CodeQL, security, UI, dependencies) |
| `docs/ux-ideas.md` | UX backlog — update on every PR |
| `docs/security-review.md` | Security posture and guidelines |
| `docs/coding-guideline-suggestions.md` | Additional coding standards |
| `docs/architecture/clean-feature-separation.md` | Architecture decisions and next steps |

## Severity key

| Tag | Meaning |
|-----|---------|
| `[BLOCKING]` | Hard stop — must be satisfied before merging |
| `[REQUIRED]` | Must be satisfied; skip only with documented justification |
| `[RECOMMENDED]` | Should be satisfied; deviations acceptable with a brief note |
| `[OPTIONAL]` | Nice to have; no justification needed if omitted |

## Core rules

**[BLOCKING][QUALITY]** All tests must pass before committing.
```
dotnet test Arbor.HttpClient.slnx
```

**[BLOCKING][SECURITY]** No secrets, tokens, or credentials in source.

**[BLOCKING][PROCESS]** Every changed line must trace directly to the user's request (no unrelated edits).

**[REQUIRED][QUALITY]** Any new or changed production code must include test coverage. Name tests `Method_Scenario_ExpectedResult`.

**[REQUIRED][QUALITY]** Treat compiler warnings and analyzer diagnostics as real defects. Never silently suppress — add a code comment with justification.

**[REQUIRED][ARCHITECTURE]** Every async method that performs I/O must accept a `CancellationToken cancellationToken` parameter and pass it through to downstream calls.

**[REQUIRED][ARCHITECTURE]** Use `throw;` (not `throw ex;`) when rethrowing exceptions to preserve the original stack trace.

**[REQUIRED][SECURITY]** Do not log sensitive data (credentials, PII, raw request bodies).

**[REQUIRED][PROCESS]** New NuGet packages:
1. Verify MIT-compatible license (see copilot-instructions.md for the compatible list).
2. Declare version in `Directory.Packages.props` — never inline in `.csproj`.
3. Document in `THIRD_PARTY_NOTICES.md`.

**[REQUIRED][ACCESSIBILITY]** UI changes: every new color pair must be covered by a test in `AccessibilityContrastTests.cs` (WCAG AA ≥ 4.5:1 for normal text).

**[REQUIRED][PROCESS]** UI changes: run E2E screenshot tests, commit output to `docs/screenshots/`, and embed screenshots inline in the PR description.

**[RECOMMENDED][PROCESS]** After each PR, write an Instruction Retrospective and propose improvements to `.github/copilot-instructions.md`.

## Structured logging fields

Use these field names consistently: `requestId`, `environment`, `jobId`, `statusCode`, `durationMs`.

Severity:
- `Information` — routine expected events
- `Warning` — unexpected but recoverable
- `Error` — failures that require attention

## Architecture guard rails

- Do **not** add new logic to `MainWindowViewModel` that belongs in a feature-specific VM.
- Wrap infrastructure (clipboard, file system, timers) behind interfaces injected into feature VMs.
- New features must have at least one focused unit test that does not require the full UI runtime.
