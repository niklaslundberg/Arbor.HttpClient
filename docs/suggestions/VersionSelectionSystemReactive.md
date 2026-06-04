# Task: Choose the version of System.Reactive to use

**Description**
- Decide which version of the `System.Reactive` NuGet package to reference for the project.
- Typically the latest stable version is preferred, but we may need to pin to a specific version for compatibility with other libraries.

**Acceptance Criteria**
1. The selected version number (e.g., `7.5.0`) is recorded at the top of this file.
2. `Directory.Packages.props` is updated with the chosen version under the `<PackageVersion>` entry for `System.Reactive`.
3. The change is reflected in the lock files (`packages.lock.json`) after a restore.
4. The CI pipeline runs `dotnet restore` successfully with no version conflicts.

**Tests to Create**
- A CI step that runs `dotnet list package --outdated` and fails if the selected version is not the latest stable (optional).