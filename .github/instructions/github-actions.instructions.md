---
applyTo: '.github/workflows/*.yml'
description: 'GitHub Actions workflow rules for Arbor.HttpClient — DRY principle, security hardening, and MSIX release conventions.'
---

# GitHub Actions Workflow Rules

> These rules apply to all `.yml` files under `.github/workflows/`. They are a targeted subset of the full guidelines in `.github/copilot-instructions.md`. When in doubt, defer to the canonical source.

## DRY — ci.yml Must Mirror release.yml

**[REQUIRED][PROCESS]** `ci.yml` must contain a `release-verification` job that mirrors every packaging step of `release.yml` **except**:
- `Attest build provenance` — requires `id-token: write`, which is not granted to CI jobs.
- `Create GitHub Release` — CI verifies the pipeline; the actual release runs only on `main`.

**[REQUIRED][PROCESS]** Any change to a shared step in `release.yml` must be applied to the corresponding step in the `release-verification` job in `ci.yml`. The two files must not diverge on shared logic.

**[REQUIRED][PROCESS]** Shared PowerShell or shell logic between `release.yml` and `ci.yml` must live in a reusable script under `scripts/` (e.g. `scripts/Build-Release.ps1`) and be invoked from both workflows. Do not duplicate logic inline.

## Security — Credentials and Permissions

**[BLOCKING][SECURITY]** All `actions/checkout` steps must include `persist-credentials: false` to prevent the workflow token from remaining in the local git config after checkout.

**[REQUIRED][SECURITY]** Grant only the minimum required `permissions` per job. Do not use top-level `permissions: write-all` or omit the `permissions` block.

**[REQUIRED][SECURITY]** Every `dotnet restore` step that runs in CI must include `--locked-mode` to prevent automatic dependency resolution from introducing untested package versions.

## Dependency Audit

**[REQUIRED][SECURITY]** Run the NuGet vulnerability audit in every build job using the full solution file:
```
dotnet list Arbor.HttpClient.slnx package --vulnerable --include-transitive
```

## SBOM Tool Directory Convention

**[RECOMMENDED][PROCESS]** `sbom-tool` always appends `_manifest` to the directory passed via `-m`. Pass `-m <build-drop-path>` (e.g. `-m publish/win-x64`) so the manifest is written to `<build-drop-path>/_manifest/spdx_2.2/manifest.spdx.json`. Never pass `-m <build-drop-path>/_manifest` — this double-nests the directory.

## Release Workflow Conventions

**[RECOMMENDED][PROCESS]** Release workflows must include a `workflow_dispatch` trigger and handle existing releases/tags idempotently (e.g. delete before re-creating) so they can be re-triggered without pushing a new commit to `main`.

**[REQUIRED][PROCESS]** MSIX manifest app identity (`NiklasLundberg.ArborHttpClient`, Publisher `CN=Arbor.HttpClient`) must remain consistent. If the `Publisher` value changes, update both `AppxManifest.xml` and the signing certificate subject.

**[REQUIRED][PROCESS]** Do not change `ProcessorArchitecture` in the MSIX manifest without also changing the `-r` runtime identifier in the `dotnet publish` step.
