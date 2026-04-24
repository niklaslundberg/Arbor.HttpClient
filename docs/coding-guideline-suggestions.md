# Suggested Additional Coding Guidelines

> **Status: Historical planning record.** All 8 suggestions below have been fully incorporated into `.github/copilot-instructions.md` (sections 7–11 and the Accessibility/Dependency sections). This file is preserved for traceability only. Do not treat it as an authoritative source of rules — always defer to `.github/copilot-instructions.md`.

This report lists potential coding guidelines that can be added incrementally on top of the current repository standards.

## 1. Analyzer severity policy

- Promote a curated subset of Roslyn/CA analyzers to `warning` (or `error`) for correctness and security-sensitive rules.
- Keep style-only rules as `suggestion` to avoid noisy CI failures.

## 2. Public API change policy

- Require documenting and reviewing public API changes (especially in `Arbor.HttpClient.Core`).
- Optionally introduce API baseline tooling if package/public API stability becomes important.

## 3. Async and cancellation conventions

- Require `CancellationToken` on async methods that can block or perform I/O.
- Require passing tokens through to downstream calls unless there is a clear reason not to.

## 4. Logging and observability conventions

- Standardize structured logging fields (request id, environment, job id, status code, duration).
- Clarify when to log at `Information` vs `Warning` vs `Error`.

## 5. Exception-handling conventions

- Prefer domain-specific exceptions (or result types) at boundaries instead of generic `Exception`.
- Preserve original exception context when rethrowing (`throw;` instead of `throw ex;`).

## 6. Test quality conventions

- Define naming patterns for tests (`Method_Scenario_ExpectedResult` or equivalent).
- Encourage one behavioral assertion per test intent and explicit arrangement of test data.

## 7. UI accessibility checklist integration

- Add a PR checklist item requiring keyboard navigation and accessible-name verification for new interactive controls.
- Require contrast-test updates whenever theme colors are introduced or changed.

## 8. Dependency governance

- Add a lightweight policy for dependency updates (frequency, owner, and CVE triage SLA).
- Require documenting license and purpose for any new dependency in `THIRD_PARTY_NOTICES.md`.
