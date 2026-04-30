using Arbor.HttpClient.Storage.Sqlite;
using Microsoft.Data.Sqlite;
using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.ScheduledJobs;

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

            await repository.SaveAsync(
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

    [Fact]
    public async Task SqliteEnvironmentRepository_SaveAndGetAllAsync_ShouldPersistSensitiveFlagAndExpiry()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"test_env_sensitive_{Guid.NewGuid()}.db");
        try
        {
            var connectionString = $"Data Source={dbPath}";
            var repository = new SqliteEnvironmentRepository(connectionString);

            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var expiry = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var variables = new List<EnvironmentVariable>
            {
                new("apiKey", "secret123", IsEnabled: true, IsSensitive: true, ExpiresAtUtc: expiry),
                new("baseUrl", "http://localhost", IsEnabled: true, IsSensitive: false, ExpiresAtUtc: null)
            };

            var environmentId = await repository.SaveAsync("Dev", variables, cancellationToken: TestContext.Current.CancellationToken);

            var environments = await repository.GetAllAsync(TestContext.Current.CancellationToken);

            environments.Should().ContainSingle();
            environments[0].Id.Should().Be(environmentId);
            environments[0].Variables.Should().HaveCount(2);

            var sensitiveVar = environments[0].Variables[0];
            sensitiveVar.Name.Should().Be("apiKey");
            sensitiveVar.IsSensitive.Should().BeTrue();
            sensitiveVar.ExpiresAtUtc.Should().NotBeNull();
            sensitiveVar.ExpiresAtUtc!.Value.Should().BeCloseTo(expiry, TimeSpan.FromSeconds(1));

            var nonSensitiveVar = environments[0].Variables[1];
            nonSensitiveVar.Name.Should().Be("baseUrl");
            nonSensitiveVar.IsSensitive.Should().BeFalse();
            nonSensitiveVar.ExpiresAtUtc.Should().BeNull();
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
    public async Task SqliteEnvironmentRepository_UpdateAsync_ShouldPersistUpdatedSensitiveFlagAndExpiry()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"test_env_sensitive_update_{Guid.NewGuid()}.db");
        try
        {
            var connectionString = $"Data Source={dbPath}";
            var repository = new SqliteEnvironmentRepository(connectionString);

            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var initialVariables = new List<EnvironmentVariable>
            {
                new("token", "old-token", IsEnabled: true, IsSensitive: false, ExpiresAtUtc: null)
            };

            var environmentId = await repository.SaveAsync("Test", initialVariables, cancellationToken: TestContext.Current.CancellationToken);

            var expiry = new DateTimeOffset(2029, 6, 15, 12, 0, 0, TimeSpan.Zero);
            var updatedVariables = new List<EnvironmentVariable>
            {
                new("token", "new-token", IsEnabled: true, IsSensitive: true, ExpiresAtUtc: expiry)
            };

            await repository.UpdateAsync(environmentId, "Test", updatedVariables, cancellationToken: TestContext.Current.CancellationToken);

            var environments = await repository.GetAllAsync(TestContext.Current.CancellationToken);

            environments.Should().ContainSingle();
            var updatedVar = environments[0].Variables[0];
            updatedVar.IsSensitive.Should().BeTrue();
            updatedVar.ExpiresAtUtc.Should().NotBeNull();
            updatedVar.ExpiresAtUtc!.Value.Should().BeCloseTo(expiry, TimeSpan.FromSeconds(1));
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
    public async Task SqliteCollectionRepository_SaveAndGetAllAsync_ShouldRoundTripNewFields()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"test_collection_newfields_{Guid.NewGuid()}.db");
        try
        {
            var connectionString = $"Data Source={dbPath}";
            var repository = new SqliteCollectionRepository(connectionString);
            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var headers = new List<RequestHeader>
            {
                new("Authorization", "Bearer {{bearerToken}}"),
                new("X-Request-ID", "{{X-Request-ID}}", IsEnabled: false)
            };

            var requests = new List<CollectionRequest>
            {
                new("Create Pet", "POST", "/pets?limit={{limit}}",
                    "Creates a pet",
                    Tag: "pets",
                    Body: """{"name":"Fluffy"}""",
                    ContentType: "application/json",
                    Headers: headers),
                new("List Pets", "GET", "/pets", "Lists pets")
            };

            var collectionId = await repository.SaveAsync("Pet API", null, "http://localhost:5000", requests, TestContext.Current.CancellationToken);

            var collections = await repository.GetAllAsync(TestContext.Current.CancellationToken);

            collections.Should().ContainSingle();
            var loaded = collections[0];
            loaded.Id.Should().Be(collectionId);
            loaded.Requests.Should().HaveCount(2);

            var createPet = loaded.Requests[0];
            createPet.Tag.Should().Be("pets");
            createPet.Body.Should().Be("""{"name":"Fluffy"}""");
            createPet.ContentType.Should().Be("application/json");
            createPet.Headers.Should().HaveCount(2);
            createPet.Headers![0].Name.Should().Be("Authorization");
            createPet.Headers![0].Value.Should().Be("Bearer {{bearerToken}}");
            createPet.Headers![1].Name.Should().Be("X-Request-ID");
            createPet.Headers![1].IsEnabled.Should().BeFalse();

            var listPets = loaded.Requests[1];
            listPets.Tag.Should().BeNull();
            listPets.Body.Should().BeNull();
            listPets.ContentType.Should().BeNull();
            listPets.Headers.Should().BeNull();
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
    public async Task SqliteCollectionRepository_GetAllAsync_ShouldReturnNullHeadersForMalformedJson()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"test_collection_badjson_{Guid.NewGuid()}.db");
        try
        {
            // Write a row with corrupt headers JSON directly via raw SQL
            var connectionString = $"Data Source={dbPath}";
            var repository = new SqliteCollectionRepository(connectionString);
            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);

            await using var insertCollection = connection.CreateCommand();
            insertCollection.CommandText =
                "INSERT INTO collections (name, source_path, base_url, created_at_utc) VALUES ('Bad', NULL, NULL, '2024-01-01'); SELECT last_insert_rowid();";
            var collectionId = (long)(await insertCollection.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;

            await using var insertRequest = connection.CreateCommand();
            insertRequest.CommandText =
                "INSERT INTO collection_requests (collection_id, name, method, path, description, notes, tag, body, content_type, headers) VALUES ($cid, 'Bad Request', 'GET', '/test', NULL, NULL, NULL, NULL, NULL, 'not-valid-json');";
            insertRequest.Parameters.AddWithValue("$cid", collectionId);
            await insertRequest.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

            var collections = await repository.GetAllAsync(TestContext.Current.CancellationToken);

            collections.Should().ContainSingle();
            collections[0].Requests.Should().ContainSingle();
            collections[0].Requests[0].Headers.Should().BeNull();
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
    public async Task SqliteCollectionRepository_UpdateAsync_ShouldRenameCollectionAndPreserveRequests()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"test_collection_rename_{Guid.NewGuid()}.db");
        try
        {
            var connectionString = $"Data Source={dbPath}";
            var repository = new SqliteCollectionRepository(connectionString);
            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var requests = new List<CollectionRequest>
            {
                new("Get Items", "GET", "/items", null)
            };

            var id = await repository.SaveAsync("Old Name", null, "http://localhost:5000", requests, TestContext.Current.CancellationToken);

            await repository.UpdateAsync(id, "New Name", null, "http://localhost:5000", requests, TestContext.Current.CancellationToken);

            var collections = await repository.GetAllAsync(TestContext.Current.CancellationToken);

            collections.Should().ContainSingle();
            collections[0].Name.Should().Be("New Name");
            collections[0].Requests.Should().ContainSingle();
            collections[0].Requests[0].Name.Should().Be("Get Items");
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

    // ── SqliteScheduledJobRepository ──────────────────────────────────────────

    [Fact]
    public async Task SqliteScheduledJobRepository_GetAllAsync_ShouldReturnEmptyListWhenNoJobs()
    {
        var connectionString = $"Data Source={Path.Join(Path.GetTempPath(), $"test_jobs_empty_{Guid.NewGuid()}.db")}";
        try
        {
            var repository = new SqliteScheduledJobRepository(connectionString);
            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var jobs = await repository.GetAllAsync(TestContext.Current.CancellationToken);

            jobs.Should().BeEmpty();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            var path = new SqliteConnectionStringBuilder(connectionString).DataSource;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SqliteScheduledJobRepository_SaveAndGetAllAsync_ShouldPersistAndRetrieveJob()
    {
        var connectionString = $"Data Source={Path.Join(Path.GetTempPath(), $"test_jobs_{Guid.NewGuid()}.db")}";
        try
        {
            var repository = new SqliteScheduledJobRepository(connectionString);
            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var config = new ScheduledJobConfig(0, "Health Check", "GET", "http://localhost:5000/health", null, null, 30, AutoStart: true);

            var id = await repository.SaveAsync(config, TestContext.Current.CancellationToken);

            var jobs = await repository.GetAllAsync(TestContext.Current.CancellationToken);

            jobs.Should().ContainSingle();
            jobs[0].Id.Should().Be(id);
            jobs[0].Name.Should().Be("Health Check");
            jobs[0].Method.Should().Be("GET");
            jobs[0].Url.Should().Be("http://localhost:5000/health");
            jobs[0].Body.Should().BeNull();
            jobs[0].HeadersJson.Should().BeNull();
            jobs[0].IntervalSeconds.Should().Be(30);
            jobs[0].AutoStart.Should().BeTrue();
            jobs[0].FollowRedirects.Should().BeNull();
            jobs[0].UseWebView.Should().BeFalse();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            var path = new SqliteConnectionStringBuilder(connectionString).DataSource;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SqliteScheduledJobRepository_SaveAsync_ShouldPersistBodyHeadersAndFollowRedirects()
    {
        var connectionString = $"Data Source={Path.Join(Path.GetTempPath(), $"test_jobs_body_{Guid.NewGuid()}.db")}";
        try
        {
            var repository = new SqliteScheduledJobRepository(connectionString);
            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var config = new ScheduledJobConfig(
                0,
                "API Ping",
                "POST",
                "http://localhost:5000/ping",
                Body: """{"alive":true}""",
                HeadersJson: """[{"Name":"Authorization","Value":"Bearer token"}]""",
                IntervalSeconds: 60,
                AutoStart: false,
                FollowRedirects: true,
                UseWebView: true);

            await repository.SaveAsync(config, TestContext.Current.CancellationToken);

            var jobs = await repository.GetAllAsync(TestContext.Current.CancellationToken);

            jobs.Should().ContainSingle();
            jobs[0].Body.Should().Be("""{"alive":true}""");
            jobs[0].HeadersJson.Should().Be("""[{"Name":"Authorization","Value":"Bearer token"}]""");
            jobs[0].FollowRedirects.Should().BeTrue();
            jobs[0].UseWebView.Should().BeTrue();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            var path = new SqliteConnectionStringBuilder(connectionString).DataSource;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SqliteScheduledJobRepository_SaveAsync_ShouldPersistFollowRedirectsFalse()
    {
        var connectionString = $"Data Source={Path.Join(Path.GetTempPath(), $"test_jobs_noredirect_{Guid.NewGuid()}.db")}";
        try
        {
            var repository = new SqliteScheduledJobRepository(connectionString);
            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var config = new ScheduledJobConfig(0, "No Redirect", "GET", "http://localhost:5000", null, null, 10, AutoStart: false, FollowRedirects: false);

            await repository.SaveAsync(config, TestContext.Current.CancellationToken);

            var jobs = await repository.GetAllAsync(TestContext.Current.CancellationToken);

            jobs.Should().ContainSingle();
            jobs[0].FollowRedirects.Should().BeFalse();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            var path = new SqliteConnectionStringBuilder(connectionString).DataSource;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SqliteScheduledJobRepository_UpdateAsync_ShouldModifyExistingJob()
    {
        var connectionString = $"Data Source={Path.Join(Path.GetTempPath(), $"test_jobs_update_{Guid.NewGuid()}.db")}";
        try
        {
            var repository = new SqliteScheduledJobRepository(connectionString);
            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var original = new ScheduledJobConfig(0, "Original", "GET", "http://localhost:5000/old", null, null, 30, AutoStart: false);
            var id = await repository.SaveAsync(original, TestContext.Current.CancellationToken);

            var updated = new ScheduledJobConfig(id, "Updated", "POST", "http://localhost:5000/new", """{"key":"val"}""", null, 60, AutoStart: true, FollowRedirects: true, UseWebView: false);
            await repository.UpdateAsync(updated, TestContext.Current.CancellationToken);

            var jobs = await repository.GetAllAsync(TestContext.Current.CancellationToken);

            jobs.Should().ContainSingle();
            jobs[0].Id.Should().Be(id);
            jobs[0].Name.Should().Be("Updated");
            jobs[0].Method.Should().Be("POST");
            jobs[0].Url.Should().Be("http://localhost:5000/new");
            jobs[0].Body.Should().Be("""{"key":"val"}""");
            jobs[0].IntervalSeconds.Should().Be(60);
            jobs[0].AutoStart.Should().BeTrue();
            jobs[0].FollowRedirects.Should().BeTrue();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            var path = new SqliteConnectionStringBuilder(connectionString).DataSource;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SqliteScheduledJobRepository_DeleteAsync_ShouldRemoveJob()
    {
        var connectionString = $"Data Source={Path.Join(Path.GetTempPath(), $"test_jobs_delete_{Guid.NewGuid()}.db")}";
        try
        {
            var repository = new SqliteScheduledJobRepository(connectionString);
            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var config = new ScheduledJobConfig(0, "Temp Job", "GET", "http://localhost:5000", null, null, 15, AutoStart: false);
            var id = await repository.SaveAsync(config, TestContext.Current.CancellationToken);

            await repository.DeleteAsync(id, TestContext.Current.CancellationToken);

            var jobs = await repository.GetAllAsync(TestContext.Current.CancellationToken);

            jobs.Should().BeEmpty();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            var path = new SqliteConnectionStringBuilder(connectionString).DataSource;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SqliteScheduledJobRepository_SaveAsync_ShouldAssignIncrementingIds()
    {
        var connectionString = $"Data Source={Path.Join(Path.GetTempPath(), $"test_jobs_multi_{Guid.NewGuid()}.db")}";
        try
        {
            var repository = new SqliteScheduledJobRepository(connectionString);
            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var id1 = await repository.SaveAsync(new ScheduledJobConfig(0, "Job A", "GET", "http://localhost:5000/a", null, null, 10, AutoStart: false), TestContext.Current.CancellationToken);
            var id2 = await repository.SaveAsync(new ScheduledJobConfig(0, "Job B", "GET", "http://localhost:5000/b", null, null, 20, AutoStart: false), TestContext.Current.CancellationToken);

            id2.Should().BeGreaterThan(id1);

            var jobs = await repository.GetAllAsync(TestContext.Current.CancellationToken);
            jobs.Should().HaveCount(2);
            jobs[0].Name.Should().Be("Job A");
            jobs[1].Name.Should().Be("Job B");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            var path = new SqliteConnectionStringBuilder(connectionString).DataSource;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SqliteScheduledJobRepository_InitializeAsync_IsIdempotent()
    {
        var connectionString = $"Data Source={Path.Join(Path.GetTempPath(), $"test_jobs_idempotent_{Guid.NewGuid()}.db")}";
        try
        {
            var repository = new SqliteScheduledJobRepository(connectionString);

            // Calling InitializeAsync twice should not throw
            await repository.InitializeAsync(TestContext.Current.CancellationToken);
            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var jobs = await repository.GetAllAsync(TestContext.Current.CancellationToken);
            jobs.Should().BeEmpty();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            var path = new SqliteConnectionStringBuilder(connectionString).DataSource;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SqliteRequestHistoryRepository_SaveAndGetRecentAsync_ShouldPersistNullBody()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"test_history_nullbody_{Guid.NewGuid()}.db");
        try
        {
            var repository = new SqliteRequestHistoryRepository(dbPath);
            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var request = new SavedRequest("No Body", "GET", "http://localhost:5000/api", null, DateTimeOffset.UtcNow);
            await repository.SaveAsync(request, TestContext.Current.CancellationToken);

            var recent = await repository.GetRecentAsync(10, TestContext.Current.CancellationToken);

            recent.Should().ContainSingle();
            recent[0].Body.Should().BeNull();
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
    public async Task SqliteRequestHistoryRepository_GetRecentAsync_ShouldOrderByMostRecentFirst()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"test_history_order_{Guid.NewGuid()}.db");
        try
        {
            var repository = new SqliteRequestHistoryRepository(dbPath);
            await repository.InitializeAsync(TestContext.Current.CancellationToken);

            var base_ = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            await repository.SaveAsync(new SavedRequest("First", "GET", "http://localhost/1", null, base_), TestContext.Current.CancellationToken);
            await repository.SaveAsync(new SavedRequest("Second", "GET", "http://localhost/2", null, base_.AddMinutes(1)), TestContext.Current.CancellationToken);
            await repository.SaveAsync(new SavedRequest("Third", "GET", "http://localhost/3", null, base_.AddMinutes(2)), TestContext.Current.CancellationToken);

            var recent = await repository.GetRecentAsync(10, TestContext.Current.CancellationToken);

            recent.Should().HaveCount(3);
            recent[0].Name.Should().Be("Third");
            recent[1].Name.Should().Be("Second");
            recent[2].Name.Should().Be("First");
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

