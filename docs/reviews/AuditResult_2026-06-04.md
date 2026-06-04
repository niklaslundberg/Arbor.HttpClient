# C# Code Audit Report

**Generated on:** 2026-06-04 (UTC)

## Check for
- Violations
- Smells
- Recommendations

### Ambiguity in the rules

### Violations

- **CancellationToken propagation**: All async I/O methods correctly accept a `CancellationToken` and pass it downstream, satisfying the hard stop rule.
- **Exception rethrowing**: No instances of `throw ex;` were found, adhering to the guideline of using `throw;`.
- **Sensitive data handling**: Exception messages do not include request bodies or credentials, complying with the no‑sensitive‑data logging rule.

### Smells

- **Redundant null checks**: Some methods perform `ArgumentNullException.ThrowIfNull` followed by a manual null‑check on the same parameter (e.g., `ArgumentNullException.ThrowIfNull(draft);` then later checking `draft.Url`). The manual check could be combined into a single validation for clarity.
- **Mixed async patterns**: `WebSocketService.ReceiveLoopAsync` swallows `OperationCanceledException` and `WebSocketException` without logging. While this avoids noisy logs, adding at least a debug log could help diagnose unexpected disconnects.
- **Hard‑coded strings**: Error messages such as `"URL must be an absolute HTTP or HTTPS URL"` are repeated across services. Consider centralizing validation messages to avoid inconsistency.

### Recommendations

- **Consolidate validation**: Create a shared helper (e.g., `ValidateUrl(string url, string paramName)`) to enforce absolute URI checks and reuse error messages.
- **Add logging for caught exceptions**: In background loops (`ReceiveLoopAsync`), log at `Debug` level when catching `OperationCanceledException` or `WebSocketException` to aid troubleshooting while still respecting the no‑sensitive‑data rule.
- **Introduce a base async service class**: Many services share patterns (HttpClient injection, cancellation token handling). A base class could reduce duplication and enforce consistent behavior.
- **Document public APIs**: While XML comments exist for many classes, some public methods lack `<returns>` documentation (e.g., `SseService.ParseSseStreamAsync`). Adding full XML docs improves IntelliSense and aligns with coding guidelines.

### Ambiguity in the rules

- The guideline *"Async methods that perform I/O must accept `CancellationToken cancellationToken` and pass it downstream"* does not specify whether optional default values are acceptable. All current implementations use an optional default (`= default`), which appears acceptable, but clarification would remove doubt for future methods.
- The rule about *"No HTTP/TLS configuration downgrade may be introduced"* is vague regarding what constitutes a downgrade. Explicitly defining prohibited changes (e.g., disabling TLS 1.2) would help reviewers.

