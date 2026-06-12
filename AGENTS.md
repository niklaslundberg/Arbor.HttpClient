# Codex Agent Instructions

> **Canonical source**: `.github/copilot-instructions.md` contains the full behavioral guidelines for this repository. Read it at the start of every session.
>
> **Sync note**: `CLAUDE.md` and `AGENTS.md` have identical body content (only the first-line title differs). If you modify the body of one, apply the same change to the other. Verify locally before committing with Bash/WSL/Git Bash: `diff <(tail -n +2 CLAUDE.md) <(tail -n +2 AGENTS.md)`; or in PowerShell: `Compare-Object (Get-Content CLAUDE.md | Select-Object -Skip 1) (Get-Content AGENTS.md | Select-Object -Skip 1)`. CI also asserts this and will fail the `agent-instructions-sync` job if the files diverge.

## Quick start

At the beginning of every session, read these files before making any decision or change:

| File | Purpose |
|------|---------|
| `.github/copilot-instructions.md` | Full behavioral guidelines (canonical) |
| `docs/reviews/implemented/review-checklist.md` | PR review items (CodeQL, security, UI, dependencies) |
| `docs/ux-ideas.md` | UX backlog — update on every PR |
| `docs/security-review.md` | Security posture and guidelines |
| `docs/coding-guideline-suggestions.md` | Historical planning record — all items incorporated into copilot-instructions.md `[OPTIONAL]` |
| `docs/architecture/clean-feature-separation.md` | Architecture decisions and next steps |
| `docs/coverage.md` | Code coverage baseline and targets — single source of truth for coverage numbers |

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
- UI changes: run `./scripts/take-screenshots.sh`, commit output to `docs/screenshots/`, embed screenshots in PR description.

## Claude Code on the web — getting the .NET SDK

The remote session container does not ship with a .NET SDK, and the default network egress policy blocks the official .NET download hosts (`builds.dotnet.microsoft.com`, `download.visualstudio.microsoft.com`), so the official `dotnet-install.sh` script fails out of the box. Use one of these paths:

1. **Automatic (preferred):** `.claude/hooks/session-start.sh` (registered in `.claude/settings.json`) runs at session start in remote sessions, installs `dotnet-sdk-10.0` from the Ubuntu apt archive, and restores NuGet packages. The hook runs **asynchronously** (up to 10 minutes) so the session starts immediately — early in a fresh session, if `dotnet` is not yet on `PATH`, the hook is still installing; wait for it to complete instead of installing in parallel.
2. **Manual fallback inside a session:** `sudo apt-get update; sudo apt-get install -y dotnet-sdk-10.0` (the `apt-get update` may report failures for unrelated third-party sources — that is fine as long as the install succeeds).
3. **Official SDK builds:** add `builds.dotnet.microsoft.com` (and `dotnet.microsoft.com`) to the Claude environment's network allowlist, then use the standard `dotnet-install.sh` with `--jsonfile global.json`.

`global.json` pins SDK `10.0.100` with `rollForward: latestFeature` so both the Ubuntu-packaged `10.0.1xx` SDKs and newer official feature bands resolve. SDK roll-forward never selects a lower version, so do not raise the pinned feature band above what the Ubuntu archive ships unless the official download hosts are allowlisted in the Claude environment.

For the full set of rules, always defer to `.github/copilot-instructions.md`.
