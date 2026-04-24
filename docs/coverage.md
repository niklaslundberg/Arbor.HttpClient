# Code Coverage

This document describes the code coverage infrastructure and current status.

## Current Coverage Status

As of the latest build (2026-04-24, after adding Kestrel integration tests for WebSocket and SSE):

- **Line coverage:** ~72% (weighted across projects)
- **Branch coverage:** ~59% (weighted across projects)

**By project** (from `dotnet test --collect:"XPlat Code Coverage"` runs):
- **Arbor.HttpClient.Core:** 84.8% line coverage (916/1080 lines), 77.9% branch coverage (346/444 branches) — from unit tests; WebSocket I/O paths additionally covered by the integration test project
- **Arbor.HttpClient.Core.Integration.Tests:** 29.8% line coverage of Core (WebSocket + SSE I/O paths), 26.75% branch — integration-only paths not reachable by unit tests
- **Arbor.HttpClient.Storage.Sqlite:** 75.3% line coverage (708/940 lines), 67.6% branch coverage (254/376 branches)
- **Arbor.HttpClient.Desktop:** 65.6% line coverage (3334/5081 lines), 51.8% branch coverage (822/1587 branches)
- **Arbor.HttpClient.Testing:** ~13% line coverage (test infrastructure — indirect coverage is acceptable)

### New code introduced in UX idea 1.3 + Kestrel integration tests

| Class | Line coverage | Notes |
|---|---|---|
| `GraphQlService` | **100%** ✅ | All paths including encoding fallback + non-JSON introspect body |
| `SseService` | **100%** ✅ | Full parser + `ConnectAsync` including header forwarding/filtering + real Kestrel SSE stream |
| `WebSocketService` — validation/state/disposal | **100%** ✅ | Covered by unit tests |
| `WebSocketService` — I/O paths | **96.4%** ✅ | `ConnectAsync` body, `SendMessageAsync`, `DisconnectAsync`, and `ReceiveLoopAsync` now covered by Kestrel integration tests; line 139 (normal while-exit without exception) is not practically reachable |
| `GraphQlDraft` | **100%** ✅ | |
| `SseEvent` | **100%** ✅ | |
| `WebSocketMessage` | **100%** ✅ | |

#### WebSocketService integration test coverage detail

The new `Arbor.HttpClient.Core.Integration.Tests` project runs a real in-process Kestrel server and covers the previously-exempt network I/O paths:

| Path | Test | Covered |
|---|---|---|
| `ConnectAsync` — connects socket, sets `IsConnected` | `ConnectAsync_WithEchoServer_SetsIsConnectedTrue` | ✅ |
| `ConnectAsync` — disposes old socket on reconnect (line 48) | `ConnectAsync_WhenCalledTwice_DisposesOldSocketAndReconnects` | ✅ |
| `ConnectAsync` — forwards custom headers (lines 51–57) | `ConnectAsync_WithCustomHeaders_ForwardsHeadersToServer` | ✅ |
| `SendMessageAsync` — sends UTF-8 text frame (lines 73–79) | `SendMessageAsync_ToEchoServer_MessageIsEchoedBack` | ✅ |
| `DisconnectAsync` — graceful close handshake (lines 88–93) | `DisconnectAsync_AfterConnect_ClosesConnectionGracefully` | ✅ |
| `ReceiveLoopAsync` — normal text frame received | `SendMessageAsync_ToEchoServer_MessageIsEchoedBack` | ✅ |
| `ReceiveLoopAsync` — multi-fragment message assembled | `ReceiveLoop_WithFragmentedMessage_AssemblesCompleteMessage` | ✅ |
| `ReceiveLoopAsync` — `OperationCanceledException` (lines 141–143) | `ReceiveLoop_WhenCancelled_ExitsCleanly` | ✅ |
| `ReceiveLoopAsync` — `WebSocketException` (lines 145–147) | `ReceiveLoop_WhenConnectionDropped_CatchesWebSocketExceptionAndExits` | ✅ |
| `ReceiveLoopAsync` — `onDisconnected` callback (line 151) | `DisconnectAsync_AfterConnect_ClosesConnectionGracefully` | ✅ |
| `ReceiveLoopAsync` — try-block normal exit (line 139) | **Exempt** — requires socket state change without a close frame; not reachable in practice |

Coverage reports are generated automatically in every CI build and available as artifacts.

## CI Integration

The CI workflow (`.github/workflows/ci.yml`) automatically:

1. Runs unit tests with code coverage collection using `coverlet.collector`
2. Generates coverage reports using ReportGenerator
3. Publishes the coverage summary to the GitHub Actions job summary
4. Uploads coverage artifacts (Cobertura XML and markdown summary) for download

## Viewing Coverage Reports

### In CI

For every CI run:

1. Navigate to the **Actions** tab in GitHub
2. Select the workflow run
3. View the **Unit Tests** job
4. The coverage summary appears at the bottom of the job summary
5. Download the `unit-test-coverage` artifact for detailed reports

### Locally

Generate a coverage report locally:

```bash
# Run tests with coverage collection
dotnet test src/Arbor.HttpClient.Core.Tests/Arbor.HttpClient.Core.Tests.csproj \
  --collect:"XPlat Code Coverage" \
  --results-directory TestResults/coverage

# Also run integration tests (WebSocket/SSE I/O paths)
dotnet test src/Arbor.HttpClient.Core.Integration.Tests/Arbor.HttpClient.Core.Integration.Tests.csproj \
  --collect:"XPlat Code Coverage" \
  --results-directory TestResults/coverage

# Install ReportGenerator (once)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report
reportgenerator \
  -reports:"TestResults/coverage/*/coverage.cobertura.xml" \
  -targetdir:"TestResults/coverage-report" \
  -reporttypes:"Html"

# Open the report
open TestResults/coverage-report/index.html  # macOS
xdg-open TestResults/coverage-report/index.html  # Linux
start TestResults/coverage-report/index.html  # Windows
```

## Coverage Targets

> **[REQUIRED]** These targets apply to **new or changed code** in each PR. Pre-existing code is measured separately.

| Project | Target | Exemptions |
|---|---|---|
| `Arbor.HttpClient.Core` | **100% line** for new/changed classes | Code paths that require live network connectivity — document in PR; use the Kestrel integration test project for I/O paths |
| `Arbor.HttpClient.Desktop` | **90% line** for new/changed ViewModels/services | UI-thread dispatch paths, Avalonia lifecycle hooks that cannot run headlessly |
| `Arbor.HttpClient.Storage.Sqlite` | **90% line** for new/changed repositories | — |
| `Arbor.HttpClient.Testing` | No minimum | Indirectly exercised through other tests |

### Core Library (Arbor.HttpClient.Core)

Current: **84.8%** line coverage (916/1080 lines), **77.9%** branch coverage (346/444 branches) — from unit test run.

WebSocket I/O paths additionally covered by `Arbor.HttpClient.Core.Integration.Tests` (Kestrel-based). All models and services now have excellent combined coverage. Remaining gaps:
- `HttpRequestService` (82.5%) - TLS negotiation and DNS lookup error paths
- `OpenApiImportService` (93.7%) - some edge cases in OpenAPI parsing
- `WebSocketService` line 139 (try-block normal exit without exception) — exempt; requires socket state to change without a WebSocket close frame, not reachable in practice

Recently improved:
- `GraphQlService`: 0% → **100%** ✅
- `SseService`: 0% → **100%** ✅
- `GraphQlDraft`, `SseEvent`, `WebSocketMessage`: 0% → **100%** ✅
- `WebSocketService` I/O paths: 0% → **96.4%** ✅ (Kestrel integration tests)

### Storage Layer (Arbor.HttpClient.Storage.Sqlite)

Current: **75.3%** line coverage (708/940 lines), **67.6%** branch coverage (254/376 branches)

### Testing Infrastructure (Arbor.HttpClient.Testing)

Current: **~13%** line coverage

This low coverage is expected because the Testing project provides test doubles and fakes used by other test projects.

## Guidelines

From `.github/copilot-instructions.md` section 7:

- Any new or changed production code must include test coverage
- Prefer isolated unit tests first, then integration/E2E tests when unit tests are not sufficient
- For I/O paths that cannot be tested with stubs (e.g. WebSocket, SSE), use the `Arbor.HttpClient.Core.Integration.Tests` project which provides a real in-process Kestrel server
- Maintain reasonably high coverage in the changed area. If code can be tested, add tests.
- For feature work, generate coverage reports and review them

## Future Improvements

1. Add coverage thresholds to enforce minimum coverage for new code
2. Implement coverage trend tracking across builds
3. Add branch-level coverage badges
4. Consider adding mutation testing for quality assessment

