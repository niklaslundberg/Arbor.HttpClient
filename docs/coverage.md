# Code Coverage

This document describes the code coverage infrastructure and current status.

## Current Coverage Status

As of the latest build (2026-04-23):

- **Line coverage:** 71% (3923 of 5523 lines)
- **Branch coverage:** 60% (1056 of 1760 branches)

**By project:**
- **Arbor.HttpClient.Core:** 90.1% line coverage, 90.1% branch coverage ✅
- **Arbor.HttpClient.Storage.Sqlite:** 91.4% line coverage, 87.9% branch coverage ✅
- **Arbor.HttpClient.Desktop:** 68.2% line coverage, 54.8% branch coverage
- **Arbor.HttpClient.Testing:** 47.5% line coverage (test infrastructure - indirect coverage acceptable)

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

### Core Library (Arbor.HttpClient.Core)

Current: **90.1%** line coverage, **90.1%** branch coverage ✅

All models and services now have excellent coverage. Remaining gaps:
- `HttpRequestService` (82.5%) - TLS negotiation and DNS lookup error paths
- `OpenApiImportService` (93.7%) - some edge cases in OpenAPI parsing

Recently improved:
- `Collection`: 66.6% → **100%** ✅
- `HttpRequestDiagnostics`: 66.6% → **100%** ✅
- `RequestEnvironment`: 0% → **100%** ✅
- `SavedRequest`: 25% → **100%** ✅
- `ScheduledJobConfig`: 20% → **100%** ✅
- `HttpRequestService`: 78.5% → **82.5%**

### Storage Layer (Arbor.HttpClient.Storage.Sqlite)

Current: **91.4%** line coverage, **87.9%** branch coverage ✅

Recently improved:
- `SqliteRequestHistoryRepository`: 0% → **100%** ✅
- `SqliteEnvironmentRepository`: 0% → **96.7%** ✅
- `SqliteCollectionRepository`: 0% → **97.8%** ✅
- `SqliteScheduledJobRepository`: Already had 70.3% coverage

All repositories now have comprehensive integration tests.

### Testing Infrastructure (Arbor.HttpClient.Testing)

Current: **47.5%** line coverage

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
