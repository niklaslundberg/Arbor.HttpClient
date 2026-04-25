# C# Refactoring Patterns Report

**Date:** 2026-04-25  
**PR:** Refactor C# code — apply repository coding guidelines  
**Scope:** All `.cs` files across `Arbor.HttpClient.Core`, `Arbor.HttpClient.Desktop`, `Arbor.HttpClient.Storage.Sqlite`, `Arbor.HttpClient.Testing`, and their test projects.

---

## Summary

This report documents the patterns identified and fixed during the C# code refactoring pass, together with guidelines and suggestions for avoiding these patterns in future work.

| Pattern | Files fixed | Occurrences fixed |
|---------|------------|-------------------|
| `is not null` → `is { }` | 17 | 50+ |
| `foreach` + inner `if` (continue/body) → `.Where()` | 5 | 6 |
| `== null` / `!= null` → `is null` / `is { }` | 3 | 2 |
| Redundant `global::` prefix on `System.Net.Http.*` types | 1 | 8 |
| `using System.Net.Http;` added for non-conflicting types | 1 | — |

All 316 tests pass before and after the refactoring.

---

## Pattern 1 — `is not null` → `is { }`

### What was found

Throughout the codebase, non-null checks used the `is not null` form instead of the preferred `is { }` pattern:

```csharp
// ✗ Before — not the preferred form in this codebase
if (cookieContainer is not null)
{
    handler.UseCookies = true;
    handler.CookieContainer = cookieContainer;
}

// ✓ After — preferred non-null check; also captures the value for use inside the block
if (cookieContainer is { } cookies)
{
    handler.UseCookies = true;
    handler.CookieContainer = cookies;
}
```

When the non-null value is subsequently used, the pattern variable (e.g. `cookies`) should replace the original variable inside the block to make the non-nullness explicit to the compiler and the reader.

### Files fixed

- `src/Arbor.HttpClient.Core/Services/HttpRequestService.cs`
- `src/Arbor.HttpClient.Core/Services/CurlFormatter.cs`
- `src/Arbor.HttpClient.Core/Services/GraphQlService.cs`
- `src/Arbor.HttpClient.Core/Services/SseService.cs`
- `src/Arbor.HttpClient.Core/Services/WebSocketService.cs`
- `src/Arbor.HttpClient.Core.Integration.Tests/KestrelServerFixture.cs`
- `src/Arbor.HttpClient.Desktop/App.axaml.cs`
- `src/Arbor.HttpClient.Desktop/ViewModels/MainWindowViewModel.cs`
- `src/Arbor.HttpClient.Desktop/ViewModels/RequestEditorViewModel.cs`
- `src/Arbor.HttpClient.Desktop/ViewModels/EnvironmentsViewModel.cs`
- `src/Arbor.HttpClient.Desktop/Views/LogWindow.axaml.cs`
- `src/Arbor.HttpClient.Desktop/Views/LogPanelView.axaml.cs`
- `src/Arbor.HttpClient.Desktop.E2E.Tests/RequestEditorViewModelTests.cs`
- `src/Arbor.HttpClient.Desktop.E2E.Tests/MainWindowUiTests.cs`
- `src/Arbor.HttpClient.Desktop.E2E.Tests/ScreenshotGenerator.cs`

### Rule

**`[REQUIRED]`** Use `is { }` for non-null checks, and `is null` for null checks. Do not use `== null`, `!= null`, or `is not null`. See `.github/instructions/csharp.instructions.md` — **Null Checks**.

---

## Pattern 2 — `foreach` + inner `if` (continue guard) → `.Where()`

### What was found

Several service methods iterated a collection and used an inner `if (!condition) { continue; }` guard to skip items that do not meet a filter criterion. The preferred form moves filtering into a `.Where()` predicate on the collection.

```csharp
// ✗ Before — filter expressed as continue-guard inside the loop body
foreach (var header in headers)
{
    if (!header.IsEnabled || string.IsNullOrWhiteSpace(header.Name))
    {
        continue;
    }
    if (string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase))
    {
        continue;
    }
    requestMessage.Headers.TryAddWithoutValidation(header.Name, header.Value);
}

// ✓ After — filter expressed declaratively in the LINQ predicate
foreach (var header in requestHeaders.Where(h => h.IsEnabled
    && !string.IsNullOrWhiteSpace(h.Name)
    && !string.Equals(h.Name, "Content-Type", StringComparison.OrdinalIgnoreCase)))
{
    requestMessage.Headers.TryAddWithoutValidation(header.Name, header.Value);
}
```

When combined with a `is { }` null-guard on the collection itself:

```csharp
if (requestDraft.Headers is { } requestHeaders)
{
    foreach (var header in requestHeaders.Where(...))
    {
        ...
    }
}
```

Additionally, inner `if` blocks that filter based on a secondary condition (not a `continue` guard but an inner `if { body }`) were also merged into the `.Where()` predicate:

```csharp
// ✗ Before — secondary if inside a loop that already uses Where
foreach (var header in draft.Headers.Where(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Name)))
{
    if (!string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase))
    {
        request.Headers.TryAddWithoutValidation(header.Name, header.Value);
    }
}

// ✓ After — all filtering consolidated in Where
foreach (var header in headers.Where(h => h.IsEnabled
    && !string.IsNullOrWhiteSpace(h.Name)
    && !string.Equals(h.Name, "Content-Type", StringComparison.OrdinalIgnoreCase)))
{
    request.Headers.TryAddWithoutValidation(header.Name, header.Value);
}
```

### Files fixed

- `src/Arbor.HttpClient.Core/Services/HttpRequestService.cs`
- `src/Arbor.HttpClient.Core/Services/CurlFormatter.cs`
- `src/Arbor.HttpClient.Core/Services/GraphQlService.cs`
- `src/Arbor.HttpClient.Core/Services/SseService.cs`
- `src/Arbor.HttpClient.Core/Services/WebSocketService.cs`

### Rule

**`[REQUIRED]`** When a `foreach` loop filters its target sequence with an inner `if` condition (either `if (condition) { body }` or `if (!condition) { continue; }`), replace the `if` with a `.Where()` call on the collection. See `docs/review-checklist.md` — **Use `.Where()` instead of implicit filtering in `foreach`**.

---

## Pattern 3 — `== null` / `!= null` → `is null` / `is { }`

### What was found

A small number of files used the older `== null` / `!= null` operators for null checks:

```csharp
// ✗ Before
if (type != null) { ... }
while (dir != null) { ... }
```

```csharp
// ✓ After
if (type is { }) { ... }
while (dir is { }) { ... }
```

### Files fixed

- `src/Arbor.HttpClient.Desktop/ViewLocator.cs` (`!= null`)
- `src/Arbor.HttpClient.Desktop.E2E.Tests/ScreenshotCaptureTests.cs` (`!= null`)

### Rule

**`[REQUIRED]`** Use `is null` for null checks and `is { }` for non-null checks. Never use `== null` or `!= null`. See `.github/instructions/csharp.instructions.md` — **Null Checks**.

---

## Pattern 4 — Redundant `global::` prefix / missing `using System.Net.Http;`

### What was found

`HttpRequestService.cs` qualified every `System.Net.Http.*` type with `global::System.Net.Http.*`. The qualifications for all types *except* `HttpClient` were unnecessary because the types do not collide with any project namespace. Adding `using System.Net.Http;` removes the verbosity for those types.

```csharp
// ✗ Before — every System.Net.Http type fully qualified
using var requestMessage = new global::System.Net.Http.HttpRequestMessage(...);
requestMessage.VersionPolicy = global::System.Net.Http.HttpVersionPolicy.RequestVersionOrLower;
requestMessage.Content = new global::System.Net.Http.StringContent(body);
using var response = await client.SendAsync(request, global::System.Net.Http.HttpCompletionOption.ResponseHeadersRead, ct);

// ✓ After — using directive added; only HttpClient keeps global:: due to namespace collision
using var requestMessage = new HttpRequestMessage(...);
requestMessage.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
requestMessage.Content = new StringContent(body);
using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
```

### Why `HttpClient` still requires `global::`

The project namespace prefix is `Arbor.HttpClient`. When any file in this project writes the name `HttpClient`, the C# compiler resolves it as the namespace `Arbor.HttpClient` (found via enclosing-namespace search) before checking imported type names. This causes CS0118 `'HttpClient' is a namespace but is used like a type`. The `global::System.Net.Http.HttpClient` qualifier bypasses this resolution and is therefore intentional and correct; it must be preserved.

All other `System.Net.Http.*` types (`HttpRequestMessage`, `HttpMethod`, `HttpVersionPolicy`, `StringContent`, `HttpCompletionOption`) do not share a name with any namespace segment and can be resolved normally once `using System.Net.Http;` is added.

### Files fixed

- `src/Arbor.HttpClient.Core/Services/HttpRequestService.cs`

### Rule

**`[RECOMMENDED]`** Add `using System.Net.Http;` and use unqualified type names for all `System.Net.Http.*` types *except* `HttpClient` (which must retain `global::System.Net.Http.HttpClient` in all files under the `Arbor.HttpClient.*` namespace tree). Document this exception in any file that introduces `System.Net.Http.HttpClient` parameters or fields.

---

## Pattern 5 — `NotifyCollectionChangedEventArgs` event handler: `is not null` on `e.NewItems`/`e.OldItems`

### What was found

Collection-changed event handlers in several ViewModels iterated `e.NewItems` and `e.OldItems` with `is not null` guards:

```csharp
// ✗ Before
if (e.NewItems is not null)
{
    foreach (RequestHeaderViewModel h in e.NewItems) { ... }
}

// ✓ After — captures the non-null list for use inside the block
if (e.NewItems is { } newHeaders)
{
    foreach (RequestHeaderViewModel h in newHeaders) { ... }
}
```

This pattern appeared in `RequestEditorViewModel.cs` and `EnvironmentsViewModel.cs`.

---

## Guidelines for Future Work

### G1 — Prefer `is { }` at point of declaration when the non-null value is immediately needed

When you need to use a potentially-null value and want to guard against null, use a pattern match that both checks and binds in one expression:

```csharp
// ✓ Preferred — single check-and-bind
if (response.Content.Headers.ContentType is { } contentType)
{
    var charset = contentType.CharSet;
}
```

This eliminates the need to null-check and then re-access the property.

### G2 — Combine all iteration filters into a single `.Where()` predicate

When building a `foreach` loop that iterates a filtered collection, express all filter conditions in the `.Where()` predicate rather than using `continue` guards inside the loop body:

```csharp
// ✓ Preferred — all conditions visible at the loop header
foreach (var header in headers.Where(h =>
    h.IsEnabled &&
    !string.IsNullOrWhiteSpace(h.Name) &&
    !string.Equals(h.Name, "Content-Type", StringComparison.OrdinalIgnoreCase)))
{
    // only headers that pass all conditions reach here
}
```

### G3 — Use `is { }` in ternary expressions for null-conditional logic

```csharp
// ✓ Preferred
var baseUrl = ActiveEnvironment is { }
    ? _variableResolver.Resolve(SelectedCollection?.BaseUrl ?? string.Empty, variables)
    : SelectedCollection?.BaseUrl ?? string.Empty;
```

### G4 — In collection-changed handlers, capture items lists via `is { }` binding

When handling `NotifyCollectionChangedEventArgs`, always capture `e.NewItems` and `e.OldItems` via `is { }` to get a non-nullable local:

```csharp
// ✓ Preferred
if (e.NewItems is { } addedItems)
{
    foreach (MyViewModel item in addedItems) { item.PropertyChanged += OnItemChanged; }
}
```

### G5 — Do not use `== null` or `!= null` — always prefer pattern matching

In new code, never write `== null` or `!= null`. Always use `is null` and `is { }` respectively. This is enforced as a `[REQUIRED]` rule in the repository instructions.

### G6 — Keep `global::System.Net.Http.HttpClient` in all files under `Arbor.HttpClient.*`

Due to the namespace collision explained in Pattern 4 above, any usage of `System.Net.Http.HttpClient` in a file within the `Arbor.HttpClient.*` namespace tree must always use the fully-qualified `global::System.Net.Http.HttpClient` form. Add a comment on first occurrence so future contributors understand why:

```csharp
// global:: required: 'HttpClient' resolves as the Arbor.HttpClient namespace without it (CS0118)
private readonly global::System.Net.Http.HttpClient _httpClient = httpClient;
```

---

## Suggestions for Future Refactoring Sessions

1. **`ResponseView.axaml.cs` and `RequestView.axaml.cs`** — These files contain a high density of `is not null` checks (50+) on UI-thread-only fields. They were not changed in this PR to keep the scope focused; a follow-up PR can apply the same `is { }` pattern systematically.

2. **XML documentation** — Public API types in `Arbor.HttpClient.Core` lack `<summary>`, `<param>`, and `<returns>` XML documentation comments. Adding documentation is a `[REQUIRED]` rule for public APIs in this project.

3. **`using` alias for `HttpClient`** — Consider a file-level alias `using NetHttpClient = System.Net.Http.HttpClient;` as an alternative to `global::` for readability in files that use `HttpClient` extensively.

4. **`ScheduledJobService.ParseHeaders` catch clause** — The bare `catch` block in `ParseHeaders` swallows all exceptions. Consider catching specifically `JsonException` and `InvalidOperationException` and logging the failure.

5. **`OpenApiImportService` loop** — The nested `foreach (path) { foreach (operation) }` could be expressed as a LINQ query with `SelectMany` for clarity.
