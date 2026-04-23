# Code Coverage

This document describes the code coverage infrastructure and current status.

## Current Coverage Status

As of the latest build:

- **Line coverage:** 57.9% (248 of 428 lines)
- **Branch coverage:** 77.9% (92 of 118 branches)

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

Current: **80.9%** line coverage, **80.7%** branch coverage

Areas needing improvement:
- `RequestEnvironment` (0%) - no tests exist
- `SavedRequest` (25%) - minimal coverage
- `ScheduledJobConfig` (20%) - minimal coverage
- `HttpResponseDetails` (71.4%) - some paths untested
- `Collection` (66.6%) - some paths untested

### Testing Infrastructure (Arbor.HttpClient.Testing)

Current: **13.1%** line coverage

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
