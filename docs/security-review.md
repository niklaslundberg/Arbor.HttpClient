# Security review (2026-04-22)

This document records the security review performed for issue **"Security review"** and should be kept as a living reference for future PRs.

## Scope reviewed

- Application code in `src/`
- CI/CD workflows in `.github/workflows/`
- NuGet dependencies and transitive dependencies
- Build and test artifacts produced by CI and release workflows

## Findings and fixes applied

### 1) Dependency vulnerability checks were not enforced in CI/release

**Risk:** Known vulnerable transitive packages could be introduced without failing automation.

**Fix applied:**

- Added an explicit NuGet vulnerability audit step to:
  - `.github/workflows/ci.yml`
  - `.github/workflows/release.yml`
- Command used:
  - `dotnet list Arbor.HttpClient.slnx package --vulnerable --include-transitive`

### 2) `actions/checkout` retained workflow token credentials by default

**Risk:** Credentials can remain in local git config during jobs, increasing blast radius if a later step is compromised.

**Fix applied:**

- Set `persist-credentials: false` for `actions/checkout` in CI and release workflows.

### 3) CI artifact retention was not explicitly minimized

**Risk:** Test artifacts can contain operational metadata and remain available longer than necessary.

**Fix applied:**

- Set `retention-days: 14` on uploaded CI artifacts in `.github/workflows/ci.yml`.

### 4) Release artifacts had no published integrity metadata

**Risk:** Consumers had no built-in checksum to verify downloaded release assets.

**Fix applied:**

- Added SHA-256 checksum generation in `.github/workflows/release.yml` for:
  - `Arbor.HttpClient.Desktop.msix`
  - `Arbor.HttpClient.Desktop.cer`
- Added checksum files as release assets:
  - `Arbor.HttpClient.Desktop.msix.sha256`
  - `Arbor.HttpClient.Desktop.cer.sha256`

## Dependency review result (current state)

- Ran: `dotnet list Arbor.HttpClient.slnx package --vulnerable --include-transitive`
- Result: no known vulnerable packages were reported from `https://api.nuget.org/v3/index.json`.

## Artifact review

### CI artifacts

- Unit test TRX/HTML reports, coverage XML, E2E TRX/HTML reports, and E2E screenshots are expected.
- Retention is now explicitly shortened to 14 days.

### Release artifacts

- Release currently includes:
  - signed MSIX package
  - sideloading `.cer` file
  - SHA-256 checksum files for both assets
- Consumers should verify checksums before installation.

## Guidelines for future PRs

1. **Keep dependency auditing mandatory**
   - Do not remove the vulnerability audit workflow steps.
   - Re-run local audit before dependency updates.

2. **Prefer least privilege in workflows**
   - Keep job/workflow permissions minimal.
   - Keep `persist-credentials: false` unless a step explicitly requires persisted git auth.

3. **Treat artifacts as security surface**
   - Upload only required artifacts.
   - Keep retention as short as practical.
   - Provide checksums for release binaries.

4. **Review security-sensitive code paths during changes**
   - HTTP/TLS configuration
   - import/export flows
   - persistence and file I/O
   - logging of request/response metadata (avoid secret leakage)

5. **Pin versions for tools and actions where practical**
   - Continue pinning dotnet tool versions.
   - Prefer immutable action references (commit SHAs) when updating workflows.
