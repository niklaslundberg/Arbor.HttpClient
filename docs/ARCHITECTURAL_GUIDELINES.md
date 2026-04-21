# Architectural Guidelines for Arbor.HttpClient

**Purpose:** This document provides practical guidelines for maintaining and extending the Arbor.HttpClient application architecture.

---

## Core Principles

### 1. Feature-Based Organization

**Organize code by feature, not by technical layer.**

✓ **Good:**
```
ViewModels/
├── Features/
│   ├── Request/
│   │   ├── RequestEditorViewModel.cs
│   │   ├── RequestHeaderViewModel.cs
│   │   └── RequestPreviewViewModel.cs
│   ├── Collections/
│   │   ├── CollectionBrowserViewModel.cs
│   │   └── CollectionItemViewModel.cs
```

✗ **Bad:**
```
ViewModels/
├── MainWindowViewModel.cs (all features mixed)
├── RequestViewModel.cs (proxy)
├── CollectionViewModel.cs (proxy)
```

**Why:** Feature-based organization allows teams to work on different features independently without merge conflicts.

---

### 2. Single Responsibility Principle (SRP)

**Each class should have one reason to change.**

✓ **Good:**
```csharp
// Only responsible for HTTP request execution
public sealed class HttpRequestService
{
    public async Task<HttpResponseModel> SendRequest(
        HttpRequestDraft request,
        CancellationToken cancellationToken = default)
    { ... }
}

// Only responsible for variable resolution
public static class VariableResolver
{
    public static string ResolveVariables(
        string input,
        IDictionary<string, string> variables)
    { ... }
}
```

✗ **Bad:**
```csharp
// Responsible for EVERYTHING (10+ responsibilities)
public class MainWindowViewModel
{
    // Request execution
    public async Task SendRequest() { ... }

    // Collection management
    public void AddCollection() { ... }

    // Environment management
    public void AddEnvironmentVariable() { ... }

    // History management
    public void FilterHistory() { ... }

    // Layout management
    public void SaveLayout() { ... }

    // ... 5 more responsibilities
}
```

**Why:** Single responsibility makes code easier to understand, test, and modify.

---

### 3. Open/Closed Principle (OCP)

**Classes should be open for extension, closed for modification.**

✓ **Good (extensible via mediator):**
```csharp
// Adding new feature: Just create new ViewModel, no modifications needed
public sealed class NewFeatureViewModel
{
    private readonly IMediator _mediator;

    public NewFeatureViewModel(IMediator mediator)
    {
        _mediator = mediator;

        // Subscribe to messages from other features
        _mediator.Subscribe<RequestExecutedMessage>(OnRequestExecuted);
    }

    private async Task DoSomethingAsync()
    {
        // Publish message to notify other features
        _mediator.Publish(new NewFeatureMessage(...));
    }
}
```

✗ **Bad (must modify existing code):**
```csharp
// Adding new feature: Must modify MainWindowViewModel
public class MainWindowViewModel
{
    // ADD NEW PROPERTIES HERE
    [ObservableProperty] private string _newFeatureData;

    // ADD NEW COMMANDS HERE
    [RelayCommand]
    private void NewFeatureAction()
    {
        // Must modify existing class for every new feature!
    }
}
```

**Why:** Modifying existing code risks breaking existing features. Extension is safer.

---

### 4. Dependency Inversion Principle (DIP)

**Depend on abstractions, not concretions.**

✓ **Good:**
```csharp
// Depends on abstraction (IRequestHistoryRepository)
public sealed class HttpRequestService
{
    private readonly IRequestHistoryRepository _historyRepository;

    public HttpRequestService(IRequestHistoryRepository historyRepository)
    {
        _historyRepository = historyRepository;
    }
}

// Easy to swap implementations (SQLite, InMemory, etc.)
services.AddSingleton<IRequestHistoryRepository, SqliteRequestHistoryRepository>();
// OR
services.AddSingleton<IRequestHistoryRepository, InMemoryRequestHistoryRepository>();
```

✗ **Bad:**
```csharp
// Depends on concrete type (SqliteRequestHistoryRepository)
public sealed class HttpRequestService
{
    private readonly SqliteRequestHistoryRepository _repository;

    public HttpRequestService(string databasePath)
    {
        _repository = new SqliteRequestHistoryRepository(databasePath);
    }
}

// Cannot swap implementation without modifying code
```

**Why:** Depending on abstractions allows flexible testing and implementation swapping.

---

## Design Patterns

### Mediator Pattern (for Feature Communication)

**Use mediator to decouple features from each other.**

```csharp
// 1. Define message
public sealed record RequestExecutedMessage(
    SavedRequest Request,
    HttpResponseModel Response,
    DateTime ExecutedAt);

// 2. Publisher (RequestEditorViewModel)
public sealed class RequestEditorViewModel
{
    private readonly IMediator _mediator;

    private async Task SendRequestAsync()
    {
        var response = await _httpRequestService.SendRequest(draft);
        _mediator.Publish(new RequestExecutedMessage(request, response, DateTime.UtcNow));
    }
}

// 3. Subscriber (HistoryBrowserViewModel)
public sealed class HistoryBrowserViewModel
{
    public HistoryBrowserViewModel(IMediator mediator)
    {
        mediator.Subscribe<RequestExecutedMessage>(OnRequestExecuted);
    }

    private void OnRequestExecuted(RequestExecutedMessage msg)
    {
        _history.Insert(0, msg.Request);
    }
}
```

**Benefits:**
- Features don't reference each other directly
- Easy to add new features that react to existing events
- Testable (can verify messages published/handled)

---

### Repository Pattern (for Data Access)

**Already well-implemented in the codebase.**

```csharp
// 1. Define abstraction in Core layer
public interface IRequestHistoryRepository
{
    Task<SavedRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SavedRequest>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(SavedRequest request, CancellationToken cancellationToken = default);
}

// 2. Implement in Storage layer
public sealed class SqliteRequestHistoryRepository : IRequestHistoryRepository
{
    public async Task SaveAsync(SavedRequest request, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        // SQLite-specific implementation
    }
}

// 3. Use in services via abstraction
public sealed class HttpRequestService
{
    public HttpRequestService(IRequestHistoryRepository historyRepository)
    {
        _historyRepository = historyRepository;
    }
}
```

**Benefits:**
- Data access logic separated from business logic
- Easy to swap database implementations
- Easy to test (use InMemory implementation)

---

### Service Layer Pattern

**Keep business logic in services, not ViewModels.**

✓ **Good:**
```csharp
// Business logic in service (testable)
public sealed class VariableResolver
{
    public static string ResolveVariables(
        string input,
        IDictionary<string, string> variables)
    {
        return VariablePattern.Replace(input, match =>
        {
            var key = match.Groups[1].Value;
            return variables.TryGetValue(key, out var value) ? value : match.Value;
        });
    }
}

// ViewModel calls service (thin)
public sealed class RequestEditorViewModel
{
    private string GetResolvedUrl()
    {
        return VariableResolver.ResolveVariables(_requestUrl, _environmentVariables);
    }
}
```

✗ **Bad:**
```csharp
// Business logic in ViewModel (hard to test)
public sealed class RequestEditorViewModel
{
    private string GetResolvedUrl()
    {
        // Complex regex logic directly in ViewModel
        var pattern = new Regex(@"\{\{([a-zA-Z0-9_]+)\}\}");
        return pattern.Replace(_requestUrl, match =>
        {
            var key = match.Groups[1].Value;
            return _environmentVariables.TryGetValue(key, out var value) ? value : match.Value;
        });
    }
}
```

**Why:** Services are reusable and easily testable. ViewModels should be thin coordinators.

---

## Testing Guidelines

### Unit Test Structure

**Follow Arrange-Act-Assert (AAA) pattern.**

```csharp
[Fact]
public async Task Should_save_request_to_history_after_execution()
{
    // Arrange
    var repository = new InMemoryRequestHistoryRepository();
    var httpClient = new HttpClient(new FakeHttpMessageHandler());
    var service = new HttpRequestService(httpClient, repository);

    var request = new HttpRequestDraft
    {
        Url = "https://example.com",
        Method = "GET"
    };

    // Act
    await service.SendRequest(request);

    // Assert
    var history = await repository.GetAllAsync();
    history.Should().HaveCount(1);
    history[0].Url.Should().Be("https://example.com");
}
```

**Guidelines:**
- One assertion per test (or closely related assertions)
- Descriptive test names (`Should_XXX_when_YYY`)
- Use builders for complex object creation
- Avoid test logic (loops, conditionals)

---

### Test Doubles (Shared Infrastructure)

**Use shared test infrastructure instead of duplicating.**

✓ **Good:**
```csharp
// Shared in Arbor.HttpClient.Testing project
using Arbor.HttpClient.Testing.Repositories;

[Fact]
public async Task Test1()
{
    var repository = new InMemoryRequestHistoryRepository();
    // ... test code
}

[Fact]
public async Task Test2()
{
    var repository = new InMemoryRequestHistoryRepository();
    // ... test code
}
```

✗ **Bad:**
```csharp
// Duplicated in every test file
public class MyTests
{
    private sealed class InMemoryRequestHistoryRepository : IRequestHistoryRepository
    {
        // 70 LOC duplicated...
    }

    [Fact]
    public async Task Test1() { ... }
}

public class OtherTests
{
    private sealed class InMemoryRequestHistoryRepository : IRequestHistoryRepository
    {
        // 70 LOC duplicated AGAIN...
    }

    [Fact]
    public async Task Test2() { ... }
}
```

**Why:** DRY (Don't Repeat Yourself) principle. Fix bugs once, applies everywhere.

---

### E2E Test Guidelines

**Use E2E tests for integration scenarios, unit tests for logic.**

```csharp
// E2E test (verifies UI integration)
[Fact]
public async Task Should_display_response_after_sending_request()
{
    // Arrange
    var session = new HeadlessUnitTestSession();
    var window = new MainWindow { DataContext = CreateViewModel() };

    // Act
    await session.ExecuteCommand(window, "SendRequest");
    await Task.Delay(100); // Wait for response

    // Assert
    var responseViewer = window.FindControl<TextEditor>("ResponseBody");
    responseViewer.Text.Should().Contain("200 OK");
}

// Unit test (verifies business logic)
[Fact]
public async Task Should_resolve_environment_variables_in_url()
{
    // Arrange
    var variables = new Dictionary<string, string>
    {
        ["baseUrl"] = "https://example.com",
        ["version"] = "v1"
    };

    // Act
    var result = VariableResolver.ResolveVariables(
        "{{baseUrl}}/api/{{version}}/users",
        variables);

    // Assert
    result.Should().Be("https://example.com/api/v1/users");
}
```

**Guidelines:**
- E2E tests: Focus on user scenarios and integration
- Unit tests: Focus on business logic and edge cases
- E2E tests are slower; unit tests are faster
- Prefer unit tests when possible

---

## Code Organization Best Practices

### Namespace Structure

**Organize namespaces by feature.**

```csharp
// Feature-based namespaces
namespace Arbor.HttpClient.Desktop.ViewModels.Features.Request;
namespace Arbor.HttpClient.Desktop.ViewModels.Features.Collections;
namespace Arbor.HttpClient.Desktop.ViewModels.Features.Environments;

// Shared/common
namespace Arbor.HttpClient.Desktop.ViewModels.Shared;
namespace Arbor.HttpClient.Desktop.ViewModels.Shell;
```

### File Organization

**One class per file, named after the class.**

✓ **Good:**
```
RequestEditorViewModel.cs   → public sealed class RequestEditorViewModel { ... }
RequestHeaderViewModel.cs   → public sealed class RequestHeaderViewModel { ... }
```

✗ **Bad:**
```
ViewModels.cs → Contains 5 different ViewModels
```

### Constructor Guidelines

**Keep constructors simple (inject dependencies only).**

✓ **Good:**
```csharp
public sealed class RequestEditorViewModel
{
    private readonly HttpRequestService _httpRequestService;
    private readonly IMediator _mediator;
    private readonly ILogger _logger;

    public RequestEditorViewModel(
        HttpRequestService httpRequestService,
        IMediator mediator,
        ILogger logger)
    {
        _httpRequestService = httpRequestService;
        _mediator = mediator;
        _logger = logger;

        // Minimal initialization only
    }
}
```

✗ **Bad:**
```csharp
public sealed class RequestEditorViewModel
{
    public RequestEditorViewModel(
        Dependency1 d1,
        Dependency2 d2,
        Dependency3 d3,
        Dependency4 d4,
        Dependency5 d5,
        Dependency6 d6,
        Dependency7 d7,
        Dependency8 d8,
        Dependency9 d9,
        Dependency10 d10,
        Dependency11 d11,
        Dependency12 d12) // 12 parameters = Constructor Hell
    {
        // Complex initialization logic
        if (d1 != null && d2 != null)
        {
            // ... 50 lines of initialization
        }
    }
}
```

**Rule of Thumb:** If constructor has >5 parameters, consider creating a facade or splitting responsibilities.

---

## Dependency Injection Guidelines

### Service Registration

**Register services by lifetime appropriately.**

```csharp
// Singleton (shared instance, stateless or carefully synchronized)
services.AddSingleton<HttpClient>();
services.AddSingleton<IRequestHistoryRepository, SqliteRequestHistoryRepository>();
services.AddSingleton<HttpRequestService>();

// Transient (new instance per request, stateful)
services.AddTransient<RequestEditorViewModel>();
services.AddTransient<CollectionBrowserViewModel>();

// Scoped (per scope/window, if needed)
// Not typically used in desktop apps
```

**Guidelines:**
- Services/Repositories: Singleton (unless stateful)
- ViewModels: Transient (each instance is independent)
- HttpClient: Singleton (reuse connection pooling)

### Avoid Service Locator

✓ **Good (Constructor Injection):**
```csharp
public sealed class RequestEditorViewModel
{
    private readonly HttpRequestService _service;

    public RequestEditorViewModel(HttpRequestService service)
    {
        _service = service;
    }
}
```

✗ **Bad (Service Locator):**
```csharp
public sealed class RequestEditorViewModel
{
    private readonly HttpRequestService _service;

    public RequestEditorViewModel()
    {
        _service = ServiceLocator.Get<HttpRequestService>(); // ANTI-PATTERN
    }
}
```

**Why:** Constructor injection makes dependencies explicit and testable.

---

## ViewModel Guidelines

### ViewModel Responsibilities

**ViewModels should:**
- Coordinate between View and Services
- Handle user input validation
- Manage UI state (selection, visibility, etc.)
- Transform data for presentation

**ViewModels should NOT:**
- Contain business logic (move to services)
- Access databases directly (use repositories)
- Contain complex algorithms (move to services)
- Know about other ViewModels directly (use mediator)

### Property Change Notifications

**Use CommunityToolkit.Mvvm source generators.**

```csharp
public partial class RequestEditorViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _requestUrl = string.Empty;

    [ObservableProperty]
    private string _requestBody = string.Empty;

    // Generates:
    // public string RequestUrl { get => _requestUrl; set => SetProperty(ref _requestUrl, value); }
    // public string RequestBody { get => _requestBody; set => SetProperty(ref _requestBody, value); }
}
```

**Guidelines:**
- Use `[ObservableProperty]` for simple properties
- Use `OnPropertyChanged()` for computed properties
- Avoid raising property changes manually (use framework)

### Command Handlers

**Use RelayCommand for user actions.**

```csharp
public partial class RequestEditorViewModel : ViewModelBase
{
    [RelayCommand]
    private async Task SendRequestAsync()
    {
        // Command implementation
        var response = await _httpRequestService.SendRequest(_draft);
        _mediator.Publish(new RequestExecutedMessage(request, response, DateTime.UtcNow));
    }

    [RelayCommand(CanExecute = nameof(CanSendRequest))]
    private async Task SendRequestAsync() { ... }

    private bool CanSendRequest() => !string.IsNullOrWhiteSpace(_requestUrl);
}
```

**Guidelines:**
- Async commands should return `Task`
- Use `CanExecute` for button enable/disable logic
- Keep command handlers thin (delegate to services)

---

## View Guidelines

### Code-Behind Rules

**Views should NOT contain business logic.**

✓ **Good:**
```csharp
// View code-behind (minimal, UI-specific only)
public partial class RequestView : UserControl
{
    public RequestView()
    {
        InitializeComponent();
    }

    // Only UI-specific event handlers
    private void OnTextBoxGotFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
            textBox.SelectAll();
    }
}
```

✗ **Bad:**
```csharp
// View code-behind (business logic - should be in ViewModel or Service)
public partial class RequestView : UserControl
{
    private void OnSendButtonClick(object? sender, RoutedEventArgs e)
    {
        // Business logic in view - BAD!
        var url = UrlTextBox.Text;
        var variables = GetEnvironmentVariables();
        var resolved = ResolveVariables(url, variables);

        var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(resolved);
        // ... 50 more lines
    }
}
```

**Why:** Business logic in views is untestable and not reusable.

---

## Performance Guidelines

### Async/Await Best Practices

```csharp
// Good: Async all the way
public async Task SendRequestAsync()
{
    var response = await _httpRequestService.SendRequest(_draft);
    await _repository.SaveAsync(savedRequest);
}

// Bad: Blocking on async (.Result, .Wait())
public void SendRequest()
{
    var response = _httpRequestService.SendRequest(_draft).Result; // DEADLOCK RISK
    _repository.SaveAsync(savedRequest).Wait(); // DEADLOCK RISK
}
```

### Collection Performance

```csharp
// Good: ObservableCollection for UI binding
public ObservableCollection<SavedRequest> History { get; } = new();

// Bad: List for UI binding (no change notifications)
public List<SavedRequest> History { get; } = new();

// Good: Batch updates
History.Clear();
foreach (var item in newItems)
    History.Add(item);

// Better: Use AddRange if available (custom collection)
public class RangeObservableCollection<T> : ObservableCollection<T>
{
    public void AddRange(IEnumerable<T> items)
    {
        foreach (var item in items)
            Items.Add(item);
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Reset));
    }
}
```

---

## Security Guidelines

### Never Hardcode Secrets

✓ **Good:**
```csharp
// Load from environment variables
var apiKey = Environment.GetEnvironmentVariable("API_KEY");

// Or from user settings
var apiKey = _applicationOptions.ApiKey;
```

✗ **Bad:**
```csharp
// Hardcoded secret - NEVER DO THIS
var apiKey = "sk_live_abc123def456";
```

### Sanitize User Input

```csharp
// Good: Validate and sanitize
public void SetRequestUrl(string url)
{
    if (string.IsNullOrWhiteSpace(url))
        throw new ArgumentException("URL cannot be empty", nameof(url));

    if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        throw new ArgumentException("Invalid URL format", nameof(url));

    _requestUrl = url;
}
```

---

## Summary Checklist

When adding new features, ask yourself:

- [ ] Is this feature isolated in its own namespace/folder?
- [ ] Does it communicate via mediator instead of direct references?
- [ ] Are dependencies injected via constructor?
- [ ] Is business logic in services, not ViewModels or Views?
- [ ] Are there unit tests for the business logic?
- [ ] Does it follow SRP (single responsibility)?
- [ ] Can I add this feature without modifying existing code (OCP)?
- [ ] Am I depending on abstractions, not concrete types (DIP)?

---

**Document Version:** 1.0
**Last Updated:** 2026-04-21
