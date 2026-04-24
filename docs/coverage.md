# Code Coverage

This document describes the code coverage infrastructure and current status.

## Current Coverage Status

As of the latest build (2026-04-24, after UX idea 1.3 — GraphQL/WebSocket/SSE/gRPC):

- **Line coverage:** ~72% (weighted across projects)
- **Branch coverage:** ~59% (weighted across projects)

**By project** (from `dotnet test --collect:"XPlat Code Coverage"` run):
- **Arbor.HttpClient.Core:** 84.8% line coverage (916/1080 lines), 77.9% branch coverage (346/444 branches)
- **Arbor.HttpClient.Storage.Sqlite:** 75.3% line coverage (708/940 lines), 67.6% branch coverage (254/376 branches)
- **Arbor.HttpClient.Desktop:** 65.6% line coverage (3334/5081 lines), 51.8% branch coverage (822/1587 branches)
- **Arbor.HttpClient.Testing:** ~13% line coverage (test infrastructure — indirect coverage is acceptable)

### New code introduced in UX idea 1.3

| Class | Line coverage | Notes |
|---|---|---|
| `GraphQlService` | **100%** ✅ | All paths including encoding fallback + non-JSON introspect body |
| `SseService` | **100%** ✅ | Full parser + `ConnectAsync` including header forwarding/filtering |
| `WebSocketService` | validation/state 100% ✅; I/O paths exempt | `ConnectAsync`, `SendAsync`, `DisconnectAsync`, and `ReceiveLoopAsync` require a live WebSocket server — untestable in isolation |
| `GraphQlDraft` | **100%** ✅ | |
| `SseEvent` | **100%** ✅ | |
| `WebSocketMessage` | **100%** ✅ | |

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
| `Arbor.HttpClient.Core` | **100% line** for new/changed classes | Code paths that require live network connectivity (e.g. WebSocket I/O) — document in PR |
| `Arbor.HttpClient.Desktop` | **90% line** for new/changed ViewModels/services | UI-thread dispatch paths, Avalonia lifecycle hooks that cannot run headlessly |
| `Arbor.HttpClient.Storage.Sqlite` | **90% line** for new/changed repositories | — |
| `Arbor.HttpClient.Testing` | No minimum | Indirectly exercised through other tests |

### Core Library (Arbor.HttpClient.Core)

Current: **84.8%** line coverage (916/1080 lines), **77.9%** branch coverage (346/444 branches)

All models and services now have excellent coverage. Remaining gaps:
- `HttpRequestService` (82.5%) - TLS negotiation and DNS lookup error paths
- `OpenApiImportService` (93.7%) - some edge cases in OpenAPI parsing
- `WebSocketService` I/O paths (0%) - `ConnectAsync`/`SendMessageAsync`/`DisconnectAsync`/`ReceiveLoopAsync` require a live WebSocket server; validation, state, and disposal are 100% covered

Recently improved:
- `Collection`: 66.6% → **100%** ✅
- `HttpRequestDiagnostics`: 66.6% → **100%** ✅
- `RequestEnvironment`: 0% → **100%** ✅
- `SavedRequest`: 25% → **100%** ✅
- `ScheduledJobConfig`: 20% → **100%** ✅
- `HttpRequestService`: 78.5% → **82.5%**
- `GraphQlService`: 0% → **100%** ✅
- `SseService`: 0% → **100%** ✅
- `GraphQlDraft`, `SseEvent`, `WebSocketMessage`: 0% → **100%** ✅

### Storage Layer (Arbor.HttpClient.Storage.Sqlite)

Current: **75.3%** line coverage (708/940 lines), **67.6%** branch coverage (254/376 branches)

Recently improved:
- `SqliteRequestHistoryRepository`: 0% → **100%** ✅
- `SqliteEnvironmentRepository`: 0% → **96.7%** ✅
- `SqliteCollectionRepository`: 0% → **97.8%** ✅
- `SqliteScheduledJobRepository`: Already had 70.3% coverage

### Testing Infrastructure (Arbor.HttpClient.Testing)

Current: **~13%** line coverage

This low coverage is expected because the Testing project provides test doubles and fakes used by other test projects. The in-memory repositories are exercised indirectly through integration tests but not through dedicated unit tests.

## Guidelines

From `.github/copilot-instructions.md` section 7:

- Any new or changed production code must include test coverage
- Prefer isolated unit tests first, then integration/E2E tests when unit tests are not sufficient
- Maintain reasonably high coverage in the changed area. If code can be tested, add tests.
- For feature work, generate coverage reports and review them

## Future Improvements

1. Add coverage thresholds to enforce minimum coverage for new code
2. Implement coverage trend tracking across builds
3. Add branch-level coverage badges
4. Consider adding mutation testing for quality assessment
