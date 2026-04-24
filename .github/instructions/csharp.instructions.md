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

**[RECOMMENDED]** Prefer domain-specific exception types (or result/discriminated-union types) at subsystem boundaries instead of leaking `System.Exception`.

## Null Checks

**[REQUIRED]** Use `is null` and `is not null` instead of `== null` and `!= null`. The pattern-based forms work correctly with overloaded equality operators and are consistent with the C# nullable reference type system.

## Member Names in Code

**[REQUIRED]** Use `nameof(member)` instead of string literals when referring to member names in log messages, exception messages, and `ArgumentException` parameter names. This ensures names stay correct after refactoring.

## Fields

**[REQUIRED]** Declare fields `readonly` whenever they are only assigned in the constructor and never mutated afterward. This is enforced by CodeQL ("Missed 'readonly' opportunity").

## Timestamps

**[RECOMMENDED]** Prefer `DateTimeOffset` over `DateTime` for timestamps. Always specify the timezone offset so timestamps are unambiguous across time zones.

## Test Methods

**[REQUIRED]** Name tests using the `Method_Scenario_ExpectedResult` pattern (e.g. `Parse_EmptyInput_ThrowsArgumentException`). Each test should verify one behavioral intent.

**[REQUIRED]** Do not emit `// Arrange`, `// Act`, or `// Assert` comments in test methods. Structure tests clearly by blank lines and variable names instead.

**[REQUIRED]** Wrap every locally created `IDisposable` in a `using` declaration or statement so it is disposed even when an exception is thrown (e.g. `using var response = new HttpResponseMessage()`).

**[REQUIRED]** Use `.Where()` instead of an inner `if` inside a `foreach` when filtering a collection. This prevents the CodeQL "Missed opportunity to use Where" diagnostic.

## Logging

**[REQUIRED]** Use structured logging fields consistently: `requestId`, `environment`, `jobId`, `statusCode`, `durationMs`.

**[REQUIRED]** Do not log sensitive data — credentials, PII, raw request bodies that may contain secrets.

Severity guidance:
- `Information` — routine, expected events (job invoked, request sent).
- `Warning` — unexpected but recoverable situations (retry attempt, fallback used).
- `Error` — failures that require attention (connection refused, unhandled exception).
