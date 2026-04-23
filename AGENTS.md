# Codex Agent Instructions

> **Canonical source**: `.github/copilot-instructions.md` contains the full behavioral guidelines for this repository. Read it at the start of every session.

## Quick start

At the beginning of every session, read these files before making any decision or change:

| File | Purpose |
|------|---------|
| `.github/copilot-instructions.md` | Full behavioral guidelines (canonical) |
| `docs/review-checklist.md` | PR review items (CodeQL, security, UI, dependencies) |
| `docs/ux-ideas.md` | UX backlog — update on every PR |
| `docs/security-review.md` | Security posture and guidelines |
| `docs/coding-guideline-suggestions.md` | Additional coding standards |
| `docs/architecture/clean-feature-separation.md` | Architecture decisions and next steps |
| `docs/coverage.md` | Code coverage baseline and guidelines |

## Hard stops (blocking rules)

- **All tests must pass** before every commit: `dotnet test Arbor.HttpClient.slnx`
- **No secrets, tokens, or credentials** may be committed to source.
- **Every changed line must trace directly** to the user's request — no unrelated edits.
- **No HTTP/TLS configuration downgrade** may be introduced.
- **No sensitive data** (credentials, PII, raw request bodies) may be logged.

## Key conventions

- Async methods that perform I/O must accept `CancellationToken cancellationToken` and pass it downstream.
- Use `throw;` (not `throw ex;`) when rethrowing to preserve the stack trace.
- New NuGet packages: verify MIT-compatible license, declare version in `Directory.Packages.props`, document in `THIRD_PARTY_NOTICES.md`.
- Test naming: `Method_Scenario_ExpectedResult` (e.g. `Parse_EmptyInput_ThrowsArgumentException`).
- New or changed production code must include test coverage; new code must not lower overall coverage below the baseline in `docs/coverage.md`.
- UI changes: run E2E screenshot tests, commit output to `docs/screenshots/`, embed screenshots in PR description.

For the full set of rules, always defer to `.github/copilot-instructions.md`.
