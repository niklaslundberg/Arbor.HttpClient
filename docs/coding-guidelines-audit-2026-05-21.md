# Coding Guidelines Audit — 2026-05-21

**Scope:** All C# source files under `src/`, `.editorconfig`, and supporting instruction files
(`copilot-instructions.md`, `csharp.instructions.md`, `review-checklist.md`).

**Methodology:** Automated pattern matching (regex, glob) across the full source tree, backed by
manual reading of representative files. Each finding traces to a specific `[REQUIRED]` or
`[RECOMMENDED]` rule in the canonical guidelines.

---

## Summary

| Severity | Count |
|---|---|
| `[REQUIRED]` violations | 6 |
| `[RECOMMENDED]` gaps | 4 |
| `.editorconfig` alignment issues | 3 |
| ✅ Fully compliant areas | 14 |

---

## Compliant Areas ✅

These patterns were checked exhaustively and found to be clean across the entire codebase.

| Rule | Source | Result |
|---|---|---|
| No bare `catch {}` / `catch (Exception) {}` without `when` | §11 / `csharp.instructions.md` | ✅ Zero violations |
| No `throw ex;` (only bare `throw;`) | §11 | ✅ Zero violations |
| File-scoped namespaces everywhere | `csharp.instructions.md` | ✅ Zero violations |
| `CancellationToken cancellationToken` (not `ct`, `token`, `cancel`) | §9 | ✅ Zero violations |
| `is null` / `is { }` — no `== null` / `!= null` in production | `csharp.instructions.md` | ✅ Zero in Core/Desktop/Storage production code |
| `DateTimeOffset.TryParse` passes `CultureInfo.InvariantCulture` | §9a | ✅ All call sites pass `InvariantCulture`; `.ToUniversalTime()` called after parse in SQLite layer |
| `IDisposable` locals wrapped with `using` in tests | `review-checklist.md` | ✅ All `HttpResponseMessage`, `MemoryStream`, etc. use `using var` |
| Integration test classes annotated with `[Trait("Category", "Integration")]` | §7 | ✅ All integration/E2E/headless test classes are annotated |
| Process-environment tests in `[Collection("ProcessEnvironment")]` with save/restore `finally` | §7 | ✅ Compliant (`SystemEnvironmentVariableProviderTests.cs`) |
| `Path.Join` for runtime-composed paths in **production code** | §12 / `csharp.instructions.md` | ✅ Core, Desktop, Storage all use `Path.Join`; no `Path.Combine` in production |
| XML documentation on Core public API types | `csharp.instructions.md` | ✅ All public classes/interfaces/records in `Arbor.HttpClient.Core` carry at least a `<summary>` |
| No `async void` except event handlers | §9 | ✅ Zero violations |
| No sync-over-async in production code | §9 / `csharp.instructions.md` | ✅ All `.GetAwaiter().GetResult()` uses are in test infrastructure callbacks (synchronous by necessity — see note under findings) |
| Vertical-slice folder structure | `architecture/clean-feature-separation.md` | ✅ Both Core and Desktop follow feature-centric organisation; no `Models/`, `Services/`, `Abstractions/` top-level directories |

---

## Required Violations 🔴

### RQ-1 · `Path.Combine` with runtime arguments in test files

**Rule:** `[REQUIRED]` — Use `Path.Join` whenever any argument is not a compile-time string
literal (`csharp.instructions.md` § Path Handling).

**Why it matters:** `Path.Combine` silently discards earlier segments when a later argument is
rooted. The guideline requires `Path.Join` everywhere, including test code — the CodeQL rule
(`cs/path-combine-user-controlled`) fires regardless of test vs production context.

**Affected files:**

| File | Lines | Pattern |
|---|---|---|
| `ApplicationOptionsStoreTests.cs` | 14, 33, 35, 36, 63, 65, 66 | `Path.Combine(Path.GetTempPath(), …)` |
| `MainWindowUiTests.cs` | 749, 1795, 1853 | `Path.Combine(Path.GetTempPath(), …)` |
| `ScreenshotGenerator.cs` | 51, 82, 115, 175, 230, 277, 336, 372, 408, 433, 458, 483, 511 | `Path.Combine(outputDir, …)` where `outputDir` is a variable |

**Fix:** Replace every call with `Path.Join`:
```csharp
// ❌ current
var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}", "options.json");

// ✅ correct
var path = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}", "options.json");
```

---

### RQ-2 · `// Arrange` / `// Act` / `// Assert` section comments in test methods

**Rule:** `[REQUIRED]` — Do not emit `// Arrange`, `// Act`, or `// Assert` comments.
Structure tests by blank lines and variable names instead (`csharp.instructions.md` § Test Methods).

**Affected locations:**

| File | Line | Offending comment |
|---|---|---|
| `GraphQlServiceTests.cs` | 235 | `// Arrange: response carries an unrecognised charset; service should not throw` |
| `GraphQlServiceTests.cs` | 259 | `// Arrange: server returns 200 with a plain-text body (not JSON)` |
| `GraphQlServiceTests.cs` | 277 | `// Arrange: a custom Content-Type header should be ignored (service sets its own)` |
| `VariableCompletionDataTests.cs` | 50 | `// Act — must not throw ArgumentOutOfRangeException` |
| `VariableCompletionDataTests.cs` | 54 | `// Assert: text is "{{host}}" and caret is at the end of the inserted text` |

**Fix:** Convert intent-explaining prose to descriptive variable names or inline assertions; delete
the `// Arrange/Act/Assert` prefix. The `GraphQlServiceTests` variants prefix a genuine
explanatory sentence with "Arrange:" — keep the sentence, strip only the prefix:
```csharp
// ❌ current
// Arrange: response carries an unrecognised charset; service should not throw
var handler = new StubHttpMessageHandler(_ => …);

// ✅ correct (sentence kept, structural marker removed)
var handler = new StubHttpMessageHandler(_ => …); // unrecognised charset — must not throw
```

---

### RQ-3 · Single-character variable names in production code

**Rule:** `[REQUIRED]` — Avoid single-character or abbreviated variable names in production code
(`csharp.instructions.md` § Member Names in Code).

**Affected file:** `src/Arbor.HttpClient.Core/OpenApiImport/OpenApiImportService.cs`

```csharp
// Line 131 — 'p' is a single-char abbreviation for OpenApiParameter
foreach (var p in pathItemParams)
{
    merged[$"{p.In}:{p.Name}"] = p;
}

// Line 136
foreach (var p in operationParams)
{
    merged[$"{p.In}:{p.Name}"] = p;
}
```

**Fix:** Rename `p` to `parameter` (or `openApiParam`) to make intent clear:
```csharp
foreach (var parameter in pathItemParams)
{
    merged[$"{parameter.In}:{parameter.Name}"] = parameter;
}
```

---

### RQ-4 · Test method naming diverges from `Method_Scenario_ExpectedResult` in `ScreenshotGenerator`

**Rule:** `[REQUIRED]` — Name tests using the `Method_Scenario_ExpectedResult` pattern
(`csharp.instructions.md` § Test Methods).

`ScreenshotGenerator` test methods are named descriptively but do not follow the convention:

| Current name | Suggested name |
|---|---|
| `GenerateInitialStateScreenshot` | `CaptureScreenshot_InitialState_SavesToDisk` |
| `GenerateAfterResponseScreenshot` | `CaptureScreenshot_AfterResponse_SavesToDisk` |
| `GenerateMainWindowScreenshot` | `CaptureScreenshot_MainWindow_SavesToDisk` |
| `GenerateVariablesScreenshot` | `CaptureScreenshot_VariablesPanel_SavesToDisk` |
| (and all remaining `Generate*` methods) | Follow same pattern |

**Note:** Because `ScreenshotGenerator` is a specialised screenshot utility rather than a
behavioural unit test, a team decision to exempt it from this rule with a documented justification
in a comment block would satisfy the `[REQUIRED]` override mechanism. Without such a justification,
a rename is required.

---

### RQ-5 · SQLite repository tests split across two test projects

**Rule:** `[REQUIRED]` — Test project boundaries must mirror library boundaries. Tests for
`Arbor.HttpClient.Storage.Sqlite` must live in `Arbor.HttpClient.Storage.Sqlite.Tests`, not in
`Arbor.HttpClient.Desktop.E2E.Tests` (`architecture/clean-feature-separation.md` §4).

**Affected file:** `src/Arbor.HttpClient.Desktop.E2E.Tests/SqliteScheduledJobRepositoryTests.cs`

This file tests `SqliteScheduledJobRepository` (from `Arbor.HttpClient.Storage.Sqlite`) but lives
in the Desktop E2E test project. An `Arbor.HttpClient.Storage.Sqlite.Tests` project already exists
and is the correct home.

**Fix:** Move `SqliteScheduledJobRepositoryTests.cs` to `Arbor.HttpClient.Storage.Sqlite.Tests/`
and update the namespace. Remove the `[AvaloniaFact]` attribute (Avalonia headless infrastructure
is not needed for pure SQLite tests) and replace with `[Fact]`.

---

### RQ-6 · Missing XML documentation on several public Core members

**Rule:** `[REQUIRED]` — Provide XML documentation for all public APIs in `Arbor.HttpClient.Core`
(`csharp.instructions.md` § XML Documentation).

The following public members lack `<summary>` comments:

| Member | File |
|---|---|
| `VariableResolver.Resolve(string, IReadOnlyList<EnvironmentVariable>)` | `Variables/VariableResolver.cs` |
| `ICollectionRepository` interface (the type itself) | `Collections/ICollectionRepository.cs` |
| `IEnvironmentRepository` interface (the type itself) | `Environments/IEnvironmentRepository.cs` |
| `IRequestHistoryRepository` interface (the type itself) | `HttpRequest/IRequestHistoryRepository.cs` |
| `CurlFormatter.Format(…)` method | `HttpRequest/CurlFormatter.cs` |

**Fix:** Add concise `<summary>` (and `<param>` / `<returns>` where applicable) to each member.
Example for the repository interfaces:
```csharp
/// <summary>
/// Defines persistence operations for HTTP collections stored by the application.
/// </summary>
public interface ICollectionRepository { … }
```

---

## Recommended Gaps 🟡

### RC-1 · Static fields missing `s_` prefix

**Rule:** `[RECOMMENDED]` — Static private/internal fields should carry the `s_` prefix (`.editorconfig`
naming rule `static_fields_should_have_prefix`).

Several static readonly fields in Core omit the prefix:

| Field | File |
|---|---|
| `PathParamPattern` | `OpenApiImport/OpenApiImportService.cs` |
| `SensitiveKeywords` | `Environments/SensitiveVariableDetector.cs` |
| `TokenPattern` | `Variables/VariableResolver.cs` |
| *(and others)* | |

Note: the `.editorconfig` sets overall naming severity to `none`
(`dotnet_analyzer_diagnostic.category-Naming.severity = none`), so this is not currently enforced
by the analyser. The recommendation is to either enforce the prefix consistently or document the
team decision to waive it.

---

### RC-2 · `.GetAwaiter().GetResult()` in test stub callbacks

**Rule:** `[REQUIRED]` — Avoid sync-over-async in production code (test code is exempt by
guideline wording).

Found in `GraphQlServiceTests.cs` (lines 23, 51, 76) and `MainWindowUiTests.cs` (line 1469)
inside `StubHttpMessageHandler` callbacks. Because the stub callback is `Func<HttpRequestMessage,
HttpResponseMessage>` (synchronous by design), there is no async alternative at the call site.

**Status:** Exempt by guideline. Flagged as informational only. No action required unless the stub
is refactored to support async handlers (`Func<…, Task<HttpResponseMessage>>`), which would
eliminate the sync-over-async entirely and is a worthwhile future improvement.

---

### RC-3 · `ScreenshotGenerator` indentation inconsistency

`ScreenshotGenerator.cs` (line ~40–53) has an extra level of indentation inside the test method
body relative to the surrounding code — likely an editing artefact:

```csharp
public async Task GenerateInitialStateScreenshot()
{
    var outputDir = …;
    Directory.CreateDirectory(outputDir);

        // App as it looks when first opened   ← 8-space indent (2 levels too deep)
        var (_, window) = CreateWindow(…);
        window.Show();
```

**Fix:** Re-indent the body to 4-space depth consistently (matches `.editorconfig`).

---

### RC-4 · Missing `<param>` tags on several Core public methods

Some Core methods have a `<summary>` but omit `<param>` for non-obvious parameters. Examples:

| Method | Missing `<param>` |
|---|---|
| `VariableResolver(ISystemEnvironmentVariableProvider)` | `environmentVariableProvider` |
| `GraphQlDraft` constructor | all named parameters |
| `SseService.ConnectAsync` | `onEvent`, `onComplete` |

The `csharp.instructions.md` asks for `<param>` on all public APIs. Adding these improves
IntelliSense discoverability.

---

## `.editorconfig` Alignment Issues ⚙️

These are gaps between the `.editorconfig` settings and the intent expressed in the guidelines.
They do not cause current violations but could allow drift to go undetected.

### EC-1 · `dotnet_style_readonly_field` severity is `suggestion`, not `warning`

```ini
dotnet_style_readonly_field = true:suggestion
```

The `[REQUIRED]` rule says all constructor-only fields must be `readonly`. A severity of
`suggestion` means IDEs show a hint but CI does not fail. Consider promoting to `:warning` so that
missed `readonly` opportunities are caught automatically in the build:

```ini
dotnet_style_readonly_field = true:warning
```

---

### EC-2 · `csharp_prefer_braces` severity is `silent`

```ini
csharp_prefer_braces = true:silent
```

The guidelines mandate explicit braces to avoid dangling-`else` bugs. A `silent` severity means
the preference is invisible in IDEs and completely unenforceable. Consider promoting to `:suggestion`
or `:warning`.

---

### EC-3 · Naming category disabled globally silences `readonly` and field-prefix rules

```ini
dotnet_analyzer_diagnostic.category-Naming.severity = none
```

This suppresses the `s_` static-field prefix rule and the `_camelCase` private-field rule
entirely. Both rules are defined in `.editorconfig` with `suggestion` severity, but that severity
is overridden to `none` by the category-level switch. As a result, naming inconsistencies produce
no signal at all in either IDE or CI. Options:

1. Remove the `category-Naming.severity = none` line and let individual rules apply at their own
   severity (recommended).
2. Keep the category override but explicitly re-enable the specific rules you care about:
   ```ini
   dotnet_analyzer_diagnostic.category-Naming.severity = none
   dotnet_diagnostic.IDE1006.severity = suggestion   # naming styles
   ```

The `CA1707` suppression for test method underscores is correct and should be retained regardless:
```ini
dotnet_diagnostic.CA1707.severity = none  # allow underscores in test names
```

---

## Recommendations Summary

| ID | Severity | Action | Effort |
|---|---|---|---|
| RQ-1 | `[REQUIRED]` | Replace `Path.Combine` → `Path.Join` in all test files | Low — mechanical find/replace |
| RQ-2 | `[REQUIRED]` | Remove `// Arrange/Act/Assert` prefixes from 5 locations | Low |
| RQ-3 | `[REQUIRED]` | Rename `p` → `parameter` in `OpenApiImportService.cs` | Low |
| RQ-4 | `[REQUIRED]` | Rename `ScreenshotGenerator` methods, or add explicit exemption comment | Low–Medium |
| RQ-5 | `[REQUIRED]` | Move `SqliteScheduledJobRepositoryTests` to the SQLite test project | Medium |
| RQ-6 | `[REQUIRED]` | Add `<summary>` (and `<param>` / `<returns>`) to 5 public Core members | Low |
| RC-1 | `[RECOMMENDED]` | Align static field naming (`s_` prefix) or document waiver | Low |
| RC-2 | `[RECOMMENDED]` | Informational — no action needed unless stub is refactored | — |
| RC-3 | `[RECOMMENDED]` | Fix indentation in `ScreenshotGenerator.cs` | Low |
| RC-4 | `[RECOMMENDED]` | Add `<param>` tags to Core methods listed above | Low |
| EC-1 | Config | Promote `dotnet_style_readonly_field` to `:warning` in `.editorconfig` | Trivial |
| EC-2 | Config | Promote `csharp_prefer_braces` to `:suggestion` or `:warning` | Trivial |
| EC-3 | Config | Remove or scope `category-Naming.severity = none` override | Low |

---

*Audit conducted against commit history as of 2026-05-21. Re-run after any significant refactor or
new feature addition.*
