using Arbor.HttpClient.Storage.Sqlite;
using Microsoft.Data.Sqlite;
using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Core.HttpRequest;

namespace Arbor.HttpClient.Storage.Sqlite.Tests;

public class SqliteRepositoriesTests
{
    [Fact]
    public async Task SqliteRequestHistoryRepository_SaveAndGetAsync_ShouldPersistAndRetrieveRequests()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"test_history_{Guid.NewGuid()}.db");
        try
        {
            var repository = new SqliteRequestHistoryRepository(dbPath);

            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var request = new SavedRequest("Test GET", "GET", "http://localhost:5000/api", null, DateTimeOffset.UtcNow);
            await repository.SaveAsync(request, TestContext.Current.CancellationToken);

            var recent = await repository.GetRecentAsync(10, TestContext.Current.CancellationToken);

            recent.Should().ContainSingle();
            recent[0].Name.Should().Be("Test GET");
            recent[0].Method.Should().Be("GET");
            recent[0].Url.Should().Be("http://localhost:5000/api");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task SqliteRequestHistoryRepository_GetRecentAsync_ShouldReturnLimitedResults()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"test_history_limit_{Guid.NewGuid()}.db");
        try
        {
            var repository = new SqliteRequestHistoryRepository(dbPath);

            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            for (int i = 0; i < 15; i++)
            {
                await repository.SaveAsync(new SavedRequest($"Request {i}", "GET", $"http://localhost:5000/{i}", null, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
            }

            var recent = await repository.GetRecentAsync(5, TestContext.Current.CancellationToken);

            recent.Should().HaveCount(5);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task SqliteEnvironmentRepository_SaveAndGetAllAsync_ShouldPersistAndRetrieveEnvironments()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"test_env_{Guid.NewGuid()}.db");
        try
        {
            var connectionString = $"Data Source={dbPath}";
            var repository = new SqliteEnvironmentRepository(connectionString);

            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var variables = new List<EnvironmentVariable>
            {
                new("apiUrl", "http://localhost:5000", IsEnabled: true),
                new("apiKey", "dev-key-123", IsEnabled: false)
            };

            var environmentId = await repository.SaveAsync("Development", variables, cancellationToken: TestContext.Current.CancellationToken);

            var environments = await repository.GetAllAsync(TestContext.Current.CancellationToken);

            environments.Should().ContainSingle();
            environments[0].Id.Should().Be(environmentId);
            environments[0].Name.Should().Be("Development");
            environments[0].Variables.Should().HaveCount(2);
            environments[0].Variables[0].Name.Should().Be("apiUrl");
            environments[0].Variables[0].Value.Should().Be("http://localhost:5000");
            environments[0].Variables[0].IsEnabled.Should().BeTrue();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task SqliteEnvironmentRepository_UpdateAsync_ShouldModifyExistingEnvironment()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"test_env_update_{Guid.NewGuid()}.db");
        try
        {
            var connectionString = $"Data Source={dbPath}";
            var repository = new SqliteEnvironmentRepository(connectionString);

            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var initialVariables = new List<EnvironmentVariable>
            {
                new("baseUrl", "http://localhost:5000/old", IsEnabled: true)
            };

            var environmentId = await repository.SaveAsync("Test", initialVariables, cancellationToken: TestContext.Current.CancellationToken);

            var updatedVariables = new List<EnvironmentVariable>
            {
                new("baseUrl", "http://localhost:5000/new", IsEnabled: true),
                new("newVar", "newValue", IsEnabled: false)
            };

            await repository.UpdateAsync(environmentId, "Test Updated", updatedVariables, cancellationToken: TestContext.Current.CancellationToken);

            var environments = await repository.GetAllAsync(TestContext.Current.CancellationToken);

            environments.Should().ContainSingle();
            environments[0].Name.Should().Be("Test Updated");
            environments[0].Variables.Should().HaveCount(2);
            environments[0].Variables.Should().Contain(v => v.Name == "newVar");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task SqliteEnvironmentRepository_DeleteAsync_ShouldRemoveEnvironment()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"test_env_delete_{Guid.NewGuid()}.db");
        try
        {
            var connectionString = $"Data Source={dbPath}";
            var repository = new SqliteEnvironmentRepository(connectionString);

            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var variables = new List<EnvironmentVariable>
            {
                new("key", "value", IsEnabled: true)
            };

            var environmentId = await repository.SaveAsync("ToDelete", variables, cancellationToken: TestContext.Current.CancellationToken);

            await repository.DeleteAsync(environmentId, TestContext.Current.CancellationToken);

            var environments = await repository.GetAllAsync(TestContext.Current.CancellationToken);

            environments.Should().BeEmpty();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task SqliteEnvironmentRepository_SaveAndGetAllAsync_ShouldPersistAccentColorAndBanner()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"test_env_color_{Guid.NewGuid()}.db");
        try
        {
            var connectionString = $"Data Source={dbPath}";
            var repository = new SqliteEnvironmentRepository(connectionString);

            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var variables = new List<EnvironmentVariable>
            {
                new("apiUrl", "http://prod.example.com", IsEnabled: true)
            };

            var environmentId = await repository.SaveAsync(
                "Production",
                variables,
                accentColor: "#B41E1E",
                showWarningBanner: true,
                cancellationToken: TestContext.Current.CancellationToken);

            var environments = await repository.GetAllAsync(TestContext.Current.CancellationToken);

            environments.Should().ContainSingle();
            environments[0].Id.Should().Be(environmentId);
            environments[0].AccentColor.Should().Be("#B41E1E");
            environments[0].ShowWarningBanner.Should().BeTrue();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task SqliteEnvironmentRepository_UpdateAsync_ShouldPersistAccentColorAndBanner()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"test_env_color_update_{Guid.NewGuid()}.db");
        try
        {
            var connectionString = $"Data Source={dbPath}";
            var repository = new SqliteEnvironmentRepository(connectionString);

            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var variables = new List<EnvironmentVariable>
            {
                new("baseUrl", "http://localhost", IsEnabled: true)
            };

            var environmentId = await repository.SaveAsync(
                "Dev",
                variables,
                cancellationToken: TestContext.Current.CancellationToken);

            await repository.UpdateAsync(
                environmentId,
                "Dev Updated",
                variables,
                accentColor: "#1E7A3C",
                showWarningBanner: false,
                cancellationToken: TestContext.Current.CancellationToken);

            var environments = await repository.GetAllAsync(TestContext.Current.CancellationToken);

            environments.Should().ContainSingle();
            environments[0].Name.Should().Be("Dev Updated");
            environments[0].AccentColor.Should().Be("#1E7A3C");
            environments[0].ShowWarningBanner.Should().BeFalse();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task SqliteEnvironmentRepository_SaveAndGetAllAsync_ShouldReturnNullAccentColorWhenNotSet()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"test_env_nocolor_{Guid.NewGuid()}.db");
        try
        {
            var connectionString = $"Data Source={dbPath}";
            var repository = new SqliteEnvironmentRepository(connectionString);

            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var environmentId = await repository.SaveAsync(
                "Neutral",
                [],
                cancellationToken: TestContext.Current.CancellationToken);

            var environments = await repository.GetAllAsync(TestContext.Current.CancellationToken);

            environments.Should().ContainSingle();
            environments[0].AccentColor.Should().BeNull();
            environments[0].ShowWarningBanner.Should().BeFalse();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task SqliteCollectionRepository_SaveAndGetAllAsync_ShouldPersistAndRetrieveCollections()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"test_collection_{Guid.NewGuid()}.db");
        try
        {
            var connectionString = $"Data Source={dbPath}";
            var repository = new SqliteCollectionRepository(connectionString);

            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var requests = new List<CollectionRequest>
            {
                new("Get All", "GET", "/items", "Retrieves all items"),
                new("Create", "POST", "/items", "Creates a new item", "Requires authentication")
            };

            var collectionId = await repository.SaveAsync("My API", "/path/to/spec", "http://localhost:5000", requests, TestContext.Current.CancellationToken);

            var collections = await repository.GetAllAsync(TestContext.Current.CancellationToken);

            collections.Should().ContainSingle();
            collections[0].Id.Should().Be(collectionId);
            collections[0].Name.Should().Be("My API");
            collections[0].SourcePath.Should().Be("/path/to/spec");
            collections[0].BaseUrl.Should().Be("http://localhost:5000");
            collections[0].Requests.Should().HaveCount(2);
            collections[0].Requests[0].Name.Should().Be("Get All");
            collections[0].Requests[1].Notes.Should().Be("Requires authentication");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task SqliteCollectionRepository_DeleteAsync_ShouldRemoveCollectionAndRequests()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"test_collection_delete_{Guid.NewGuid()}.db");
        try
        {
            var connectionString = $"Data Source={dbPath}";
            var repository = new SqliteCollectionRepository(connectionString);

            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var requests = new List<CollectionRequest>
            {
                new("Test Request", "GET", "/test", null)
            };

            var collectionId = await repository.SaveAsync("Test Collection", null, null, requests, TestContext.Current.CancellationToken);

            await repository.DeleteAsync(collectionId, TestContext.Current.CancellationToken);

            var collections = await repository.GetAllAsync(TestContext.Current.CancellationToken);

            collections.Should().BeEmpty();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task SqliteCollectionRepository_UpdateAsync_ShouldReplaceNameAndRequests()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"test_collection_update_{Guid.NewGuid()}.db");
        try
        {
            var connectionString = $"Data Source={dbPath}";
            var repository = new SqliteCollectionRepository(connectionString);
            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var id = await repository.SaveAsync("Original Name", null, "http://localhost:5000",
            [
                new CollectionRequest("Old Request", "GET", "/old", null)
            ], TestContext.Current.CancellationToken);

            var updatedRequests = new List<CollectionRequest>
            {
                new("New Request A", "POST", "/new-a", "First new request"),
                new("New Request B", "DELETE", "/new-b", "Second new request")
            };

            await repository.UpdateAsync(id, "Updated Name", "/path/to/spec", "http://localhost:5000/updated", updatedRequests, TestContext.Current.CancellationToken);

            var collections = await repository.GetAllAsync(TestContext.Current.CancellationToken);
            collections.Should().HaveCount(1);
            collections[0].Name.Should().Be("Updated Name");
            collections[0].SourcePath.Should().Be("/path/to/spec");
            collections[0].BaseUrl.Should().Be("http://localhost:5000/updated");
            collections[0].Requests.Should().HaveCount(2);
            collections[0].Requests[0].Name.Should().Be("New Request A");
            collections[0].Requests[1].Name.Should().Be("New Request B");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task SqliteCollectionRepository_UpdateAsync_ShouldClearRequestsWhenListIsEmpty()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"test_collection_update_empty_{Guid.NewGuid()}.db");
        try
        {
            var connectionString = $"Data Source={dbPath}";
            var repository = new SqliteCollectionRepository(connectionString);
            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var id = await repository.SaveAsync("My Collection", null, null,
            [
                new CollectionRequest("Request", "GET", "/test", null)
            ], TestContext.Current.CancellationToken);

            await repository.UpdateAsync(id, "My Collection", null, null, [], TestContext.Current.CancellationToken);

            var collections = await repository.GetAllAsync(TestContext.Current.CancellationToken);
            collections.Should().HaveCount(1);
            collections[0].Requests.Should().BeEmpty();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }
}
