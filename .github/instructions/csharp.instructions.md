---
applyTo: '**/*.cs'
description: 'C#-specific coding rules for Arbor.HttpClient — async/await, exceptions, testing, and style conventions.'
---

# C# Coding Rules

> These rules apply to all `.cs` files. They are a targeted subset of the full guidelines in `.github/copilot-instructions.md`. When in doubt, defer to the canonical source.

## Async and Cancellation

**[REQUIRED]** Every async method that performs I/O or can block must accept a `CancellationToken cancellationToken` parameter (not `ct` or any other abbreviation — use the full name for BCL consistency) and pass it through to all downstream async calls.

Exception: fire-and-forget cleanup paths may omit the token, but must document why.

## Exception Handling

**[REQUIRED]** When rethrowing an exception, use bare `throw;` — never `throw ex;`. The bare form preserves the original stack trace.

**[REQUIRED]** Catch only exceptions you can handle meaningfully. Let the rest propagate to a top-level handler.

**[REQUIRED]** Never use a bare `catch {}` clause or `catch (Exception) {}` without a `when` filter — these are CodeQL `cs/catch-of-all-exceptions` violations. Always specify the concrete exception type(s) you expect. When multiple unrelated types must share one catch block, use a `when` predicate:

```csharp
// ✅ specific types via when predicate
catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
{
    // silently ignore: no default browser
}

// ❌ too broad — triggers CodeQL cs/catch-of-all-exceptions
catch { }
catch (Exception) { }
```

**[REQUIRED]** Remove `try/catch` wrappers entirely when the enclosed code cannot realistically throw (e.g. generated `ObservableProperty` setters, string interpolation, trivial property reads). Defensive catches that swallow real bugs are worse than no catch at all.

**[RECOMMENDED]** Prefer domain-specific exception types (or result/discriminated-union types) at subsystem boundaries instead of leaking `System.Exception`.

## Path Handling

**[REQUIRED]** Use `Path.Join` instead of `Path.Combine` whenever any argument is not a compile-time string literal. `Path.Combine` silently discards its earlier arguments when a later argument is rooted (starts with `/` or a drive letter) — this is CodeQL `cs/path-combine-user-controlled`. `Path.Join` never silently drops arguments.

```csharp
// ✅ safe
var dbPath = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");

// ❌ may silently discard the temp path — CodeQL flags this
var dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
```

`Path.Combine` is only acceptable when **all** arguments are compile-time string literals (e.g. `Path.Combine("docs", "screenshots", "main.png")`).

## Null Checks

**[REQUIRED]** Use `is null` for null checks and `is { }` for non-null checks instead of `== null` / `!= null` / `is not null`. The pattern-based forms work correctly with overloaded equality operators, are consistent with the C# nullable reference type system, and `is { }` is preferred over `is not null` in this codebase.

## Member Names in Code

**[REQUIRED]** Use `nameof(member)` instead of string literals when referring to member names in log messages, exception messages, and `ArgumentException` parameter names. This ensures names stay correct after refactoring.

## Fields

**[REQUIRED]** Declare fields `readonly` whenever they are only assigned in the constructor and never mutated afterward. This is enforced by CodeQL ("Missed 'readonly' opportunity").

## Timestamps

**[RECOMMENDED]** Prefer `DateTimeOffset` over `DateTime` for timestamps. Always specify the timezone offset so timestamps are unambiguous across time zones.

## Date and Time Parsing

**[REQUIRED]** Always pass `CultureInfo.InvariantCulture` (never `null`) to `DateTimeOffset.TryParse`, `DateTimeOffset.Parse`, `DateTime.TryParse`, and similar parsing methods whenever the input is expected to be in ISO 8601 or any other culture-independent format. Passing `null` silently falls back to the current thread culture, which can cause parsing failures or incorrect results on non-English systems:

```csharp
// ✅ culture-invariant — safe on all locales
DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)

// ❌ uses current thread culture — silently breaks on non-English locales
DateTimeOffset.TryParse(value, null, DateTimeStyles.RoundtripKind, out var parsed)
```

**[REQUIRED]** When materialising a stored UTC timestamp (e.g. from SQLite), call `.ToUniversalTime()` after parsing to normalise the offset to UTC, regardless of what offset the stored string may carry.

## Test Methods

**[REQUIRED]** Name tests using the `Method_Scenario_ExpectedResult` pattern (e.g. `Parse_EmptyInput_ThrowsArgumentException`). Each test should verify one behavioral intent.

**[REQUIRED]** Do not emit `// Arrange`, `// Act`, or `// Assert` comments in test methods. Structure tests clearly by blank lines and variable names instead.

**[REQUIRED]** Wrap every locally created `IDisposable` in a `using` declaration or statement so it is disposed even when an exception is thrown (e.g. `using var response = new HttpResponseMessage()`).

**[REQUIRED]** Use `.Where()` instead of an inner `if` inside a `foreach` when filtering a collection. This prevents the CodeQL "Missed opportunity to use Where" diagnostic.

**[REQUIRED]** Use `.Any()` instead of a `foreach + if + return true / return false` pattern for existence checks. This prevents the same CodeQL diagnostic and is more readable:

```csharp
// ✅ idiomatic
return keywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));

// ❌ triggers CodeQL — use .Any() instead
foreach (var k in keywords)
{
    if (name.Contains(k, StringComparison.OrdinalIgnoreCase))
        return true;
}
return false;
```

**[REQUIRED]** Combine nested `if` statements into a single compound condition when there is no intermediate logic between them. Nested `if`s trigger the CodeQL "Nested 'if' statements can be combined" diagnostic:

```csharp
// ✅ combined
if (conditionA && conditionB) { ... }

// ❌ triggers CodeQL
if (conditionA)
{
    if (conditionB) { ... }
}
```

## Logging

**[REQUIRED]** Use structured logging fields consistently: `requestId`, `environment`, `jobId`, `statusCode`, `durationMs`.

**[REQUIRED]** Do not log sensitive data — credentials, PII, raw request bodies that may contain secrets.

Severity guidance:
- `Information` — routine, expected events (job invoked, request sent).
- `Warning` — unexpected but recoverable situations (retry attempt, fallback used).
- `Error` — failures that require attention (connection refused, unhandled exception).

## Namespaces

**[REQUIRED]** Use file-scoped namespace declarations (`namespace MyNamespace;`) for all new `.cs` files. Keep namespace style consistent within a file.

## Nullable Reference Types

**[REQUIRED]** Enable nullable reference types globally via `<Nullable>enable</Nullable>` in `Directory.Build.props`. Do **not** use per-file `#nullable enable` directives — the global setting applies uniformly across the solution.

## Type Declarations

**[RECOMMENDED]** Use `var` for local variables when the type is unambiguously clear from the right-hand side (e.g. `var list = new List<string>()`). Use explicit types for method parameters, return types, and field declarations.

## String Handling

**[RECOMMENDED]** Prefer string interpolation (`$""`) over `string.Format()` or concatenation. Use `StringBuilder` for heavy or repeated string construction. Use `string.Equals(a, b, StringComparison)` for culture-aware comparisons.

## Collections

**[RECOMMENDED]** Use `IEnumerable<T>` for method parameters when only enumeration is needed. Expose immutable data via `IReadOnlyList<T>` or `IReadOnlyCollection<T>`. Initialize collections with a known capacity when the size is predictable.

**[RECOMMENDED]** Prefer collection expressions (`[item1, item2]`, `[..existingCollection, newItem]`) over explicit constructor calls or `new List<T> { }` initializers where the target type can be inferred. Collection expressions are more concise and work uniformly with arrays, lists, spans, and immutable collections.

## LINQ

**[RECOMMENDED]** Prefer method syntax over query syntax for simple operations. Be aware of deferred execution — call `ToList()` or `ToArray()` when the sequence will be enumerated multiple times.

## Object-Oriented Design

**[RECOMMENDED]** Prefer composition over inheritance. Follow SOLID principles: single responsibility, open/closed, Liskov substitution, interface segregation, dependency inversion.

## XML Documentation

**[REQUIRED]** Provide XML documentation comments for all public APIs in `Arbor.HttpClient.Core` (`<summary>`, `<param>`, `<returns>`, `<exception>`).

**[RECOMMENDED]** Document public APIs in other projects. Include `<example>` sections for non-obvious methods and note thread-safety assumptions.

## File Organisation

**[RECOMMENDED]** One public type per file; file name matches the primary type name. Order `using` directives: System namespaces first, then third-party, then project namespaces. Remove unused `using` statements.

## Post-Edit Hygiene

**[RECOMMENDED]** After any code change: trim trailing whitespace from all modified lines, ensure no extra blank lines at the end of the file, and verify indentation uses 4 spaces (no tabs).

## Resource Management

**[RECOMMENDED]** Implement `IDisposable` for types that own unmanaged resources. Follow the standard Dispose pattern. Add finalizers only when the type directly owns a native handle with no safe-handle wrapper.
