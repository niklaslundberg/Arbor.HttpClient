# Actionable Recommendation – Synchronize Package Versions in Documentation

**Target files:**
- `Directory.Packages.props` (source of truth for package versions)
- `THIRD_PARTY_NOTICES.md` (human‑readable license and version list)

## Action
1. Write a small script (PowerShell or Bash) that parses `Directory.Packages.props` to extract each package name and version.
2. For each entry, locate the matching line in `THIRD_PARTY_NOTICES.md` and replace the version number with the one from the props file.
3. Run the script as part of the CI pipeline (e.g., a `dotnet format` step) to fail the build if any mismatch remains.
4. Commit the updated `THIRD_PARTY_NOTICES.md`.

## Expected Outcome
- All version numbers in the public notice file will match the actual versions used by the build.
- Reduces the risk of license‑compliance confusion and keeps the documentation accurate.

## Estimated Effort
- **Developer time:** ~30 minutes to write and test the script.
- **Testing:** Add a unit test for the script (or a CI check) that asserts no mismatches.

## Expected Effect on Codebase
- Improves reliability of the `THIRD_PARTY_NOTICES.md` artifact.
- No runtime impact; purely a documentation‑consistency fix.
