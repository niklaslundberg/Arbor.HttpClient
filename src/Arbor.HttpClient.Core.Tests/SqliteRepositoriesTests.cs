using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Storage.Sqlite;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;

namespace Arbor.HttpClient.Core.Tests;

public class SqliteRepositoriesTests
{
    [Fact]
    public async Task SqliteRequestHistoryRepository_SaveAndGetAsync_ShouldPersistAndRetrieveRequests()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"test_history_{Guid.NewGuid()}.db");
        try
        {
            var repository = new SqliteRequestHistoryRepository(dbPath);

            await repository.InitializeAsync();

            var request = new SavedRequest("Test GET", "GET", "https://example.com/api", null, DateTimeOffset.UtcNow);
            await repository.SaveAsync(request);

            var recent = await repository.GetRecentAsync(10);

            recent.Should().ContainSingle();
            recent[0].Name.Should().Be("Test GET");
            recent[0].Method.Should().Be("GET");
            recent[0].Url.Should().Be("https://example.com/api");
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

            await repository.InitializeAsync();

            for (int i = 0; i < 15; i++)
            {
                await repository.SaveAsync(new SavedRequest($"Request {i}", "GET", $"https://example.com/{i}", null, DateTimeOffset.UtcNow));
            }

            var recent = await repository.GetRecentAsync(5);

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

            await repository.InitializeAsync();

            var variables = new List<EnvironmentVariable>
            {
                new("apiUrl", "https://api.dev.example.com", IsEnabled: true),
                new("apiKey", "dev-key-123", IsEnabled: false)
            };

            var environmentId = await repository.SaveAsync("Development", variables);

            var environments = await repository.GetAllAsync();

            environments.Should().ContainSingle();
            environments[0].Id.Should().Be(environmentId);
            environments[0].Name.Should().Be("Development");
            environments[0].Variables.Should().HaveCount(2);
            environments[0].Variables[0].Name.Should().Be("apiUrl");
            environments[0].Variables[0].Value.Should().Be("https://api.dev.example.com");
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

            await repository.InitializeAsync();

            var initialVariables = new List<EnvironmentVariable>
            {
                new("baseUrl", "https://old.example.com", IsEnabled: true)
            };

            var environmentId = await repository.SaveAsync("Test", initialVariables);

            var updatedVariables = new List<EnvironmentVariable>
            {
                new("baseUrl", "https://new.example.com", IsEnabled: true),
                new("newVar", "newValue", IsEnabled: false)
            };

            await repository.UpdateAsync(environmentId, "Test Updated", updatedVariables);

            var environments = await repository.GetAllAsync();

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

            await repository.InitializeAsync();

            var variables = new List<EnvironmentVariable>
            {
                new("key", "value", IsEnabled: true)
            };

            var environmentId = await repository.SaveAsync("ToDelete", variables);

            await repository.DeleteAsync(environmentId);

            var environments = await repository.GetAllAsync();

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
    public async Task SqliteCollectionRepository_SaveAndGetAllAsync_ShouldPersistAndRetrieveCollections()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"test_collection_{Guid.NewGuid()}.db");
        try
        {
            var connectionString = $"Data Source={dbPath}";
            var repository = new SqliteCollectionRepository(connectionString);

            await repository.InitializeAsync();

            var requests = new List<CollectionRequest>
            {
                new("Get All", "GET", "/items", "Retrieves all items"),
                new("Create", "POST", "/items", "Creates a new item", "Requires authentication")
            };

            var collectionId = await repository.SaveAsync("My API", "/path/to/spec", "https://api.example.com", requests);

            var collections = await repository.GetAllAsync();

            collections.Should().ContainSingle();
            collections[0].Id.Should().Be(collectionId);
            collections[0].Name.Should().Be("My API");
            collections[0].SourcePath.Should().Be("/path/to/spec");
            collections[0].BaseUrl.Should().Be("https://api.example.com");
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

            await repository.InitializeAsync();

            var requests = new List<CollectionRequest>
            {
                new("Test Request", "GET", "/test", null)
            };

            var collectionId = await repository.SaveAsync("Test Collection", null, null, requests);

            await repository.DeleteAsync(collectionId);

            var collections = await repository.GetAllAsync();

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
}
