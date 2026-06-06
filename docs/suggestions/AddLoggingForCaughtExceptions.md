# Actionable Recommendation – Add Logging for Caught Exceptions

**Target file:** `src/Arbor.HttpClient.Core/Services/WebSocketService.cs` (method `ReceiveLoopAsync`).

## Action
1. Inject a logger (e.g., `ILogger<WebSocketService>` via constructor).
2. In the `catch (OperationCanceledException)` block, log at **Debug** level:
   ```csharp
   _logger?.LogDebug(e, "WebSocket receive loop cancelled.");
   ```
3. In the `catch (WebSocketException)` block, log at **Debug** (or **Warning** if you want visibility) without exposing sensitive data:
   ```csharp
   _logger?.LogDebug(e, "WebSocket receive loop encountered a protocol error.");
   ```
4. Ensure the logger is registered in the DI container.

## Expected Outcome
- Diagnostic visibility into why a WebSocket connection stopped unexpectedly.
- No change to functional behaviour; only additional telemetry.
- Conforms to the audit suggestion *"Add logging for caught exceptions"*.

## Estimated Effort
- **Developer time:** ~30‑45 minutes.
- **Testing:** Add a unit test that forces a `WebSocketException` (by mocking `ClientWebSocket`) and asserts that the logger receives a `LogLevel.Debug` entry.

## Expected Effect on Codebase
- Improves observability and troubleshooting for production issues.
- Aligns with the repository rule that background loops should log caught exceptions at a suitable level while still respecting the *no‑sensitive‑data* rule.
