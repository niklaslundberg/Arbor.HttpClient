# Copilot Instructions

## License Compatibility

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

## MSIX Packaging and Releases

The desktop application (`Arbor.HttpClient.Desktop`) is distributed as a signed MSIX package.

### How it works

- The MSIX manifest template lives at `src/Arbor.HttpClient.Desktop/packaging/AppxManifest.xml`.
- `VERSION_PLACEHOLDER` in the manifest is substituted at build time with the 4-part version derived from the GitHub Actions run number (`1.0.{run_number}.0`).
- The release workflow (`.github/workflows/release.yml`) runs automatically on every push to `main`. It:
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

## Central Package Management

All NuGet package versions are managed centrally in `Directory.Packages.props`.  
- Always declare new packages with `<PackageVersion Include="PackageName" Version="x.y.z" />` in `Directory.Packages.props`.  
- Reference packages in `.csproj` files using `<PackageReference Include="PackageName" />` **without** a `Version` attribute.  
- Shared MSBuild properties (`TargetFramework`, `Nullable`, `ImplicitUsings`, etc.) live in `Directory.Build.props` and are inherited by all projects automatically.
