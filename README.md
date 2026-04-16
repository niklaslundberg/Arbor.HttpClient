# Arbor.HttpClient

Cross-platform desktop HTTP client built with **.NET 10**, **Avalonia 12**, and **SQLite**.

## Solution layout

- `Arbor.HttpClient.slnx` (SLNX solution at repo root)
- `src/Arbor.HttpClient.Core` - UI-agnostic business logic for sending requests and storing history
- `src/Arbor.HttpClient.Storage.Sqlite` - SQLite implementation of request history storage
- `src/Arbor.HttpClient.Desktop` - Avalonia desktop UI app (Windows/Linux/macOS)
- `src/Arbor.HttpClient.Core.Tests` - xUnit + AwesomeAssertions unit tests
- `src/Arbor.HttpClient.Desktop.E2E.Tests` - headless UI automation tests

## Run

```bash
dotnet run --project src/Arbor.HttpClient.Desktop/Arbor.HttpClient.Desktop.csproj
```

## Test

```bash
dotnet test Arbor.HttpClient.slnx
```
