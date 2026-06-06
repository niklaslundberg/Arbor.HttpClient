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

## Future — Sensitive Variable Encryption

The `IsSensitive` flag now marks variables for masking in the UI, but values are still stored as plaintext in the SQLite database. The following guidance describes how to add at-rest encryption in a future PR.

### Recommended approach: OS-managed credential store

The most secure and operationally simple approach is to **not store sensitive values in the SQLite database at all**, and instead delegate to the OS-provided credential store:

| Platform | API |
|---|---|
| Windows | `Windows.Security.Credentials.PasswordVault` (WinRT) or `System.Security.Cryptography.ProtectedData` (DPAPI) |
| macOS | `Security.SecKeychain` (via `Interop.Apple.Security`) or the `Keychain` APIs |
| Linux | `libsecret` / GNOME Keyring, or `Secret Service API` via `DBus` |

**How to integrate:**
1. Introduce an `ISecretStore` interface in `Arbor.HttpClient.Core` with `SaveAsync(string key, string secret)`, `GetAsync(string key)`, and `DeleteAsync(string key)`.
2. Implement platform-specific stores in `Arbor.HttpClient.Desktop` (using `RuntimeInformation.IsOSPlatform`).
3. When saving a sensitive variable, store a lookup key (e.g. `env:{environmentId}:{variableName}`) in SQLite instead of the plaintext value, and call `ISecretStore.SaveAsync` with the actual value.
4. When loading, detect the lookup-key sentinel and call `ISecretStore.GetAsync` to retrieve the real value before passing to the resolver.

### Alternative approach: application-level AES encryption

If an OS credential store is not available or desired, AES-256-GCM encryption can be applied at the application layer:

1. Derive a key from a user-supplied passphrase using PBKDF2 (`Rfc2898DeriveBytes` with SHA-512, ≥ 310 000 iterations per OWASP 2024).
2. Generate a random 96-bit (12-byte) nonce per encrypted value.
3. Use `System.Security.Cryptography.AesGcm` to encrypt; store `base64(nonce) + ":" + base64(ciphertext + tag)` in the SQLite `value` column.
4. Add an `is_encrypted` column to `environment_variables` to distinguish plaintext from encrypted values during migration.
5. Prompt for the passphrase on application startup (or on first access of a sensitive variable) and cache the derived key in memory only.

**Note:** Key management is the hardest part. A passphrase stored alongside the encrypted data provides no security. Only use this approach if the key is kept separate from the database (e.g. derived from user input, fetched from OS keychain, or injected via environment variable).

### External secret sources (future)

For teams or power users, sensitive variable values can be sourced from:

- **HashiCorp Vault** — authenticate with a token or AppRole and call `/v1/secret/data/{path}` to read a KV secret.
- **Azure Key Vault** — use the `Azure.Security.KeyVault.Secrets` SDK with DefaultAzureCredential.
- **AWS Secrets Manager** — use the `AWSSDK.SecretsManager` SDK.
- **OS Keychain** — see platform-specific APIs above.

Implement each source as a class implementing `ISecretStore` and let the user configure which backend is used per environment (e.g. an `EnvironmentSecretBackend` field in `RequestEnvironment`).

