using System.Text;
using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.OpenApiImport;
using Arbor.HttpClient.Desktop.Features.Collections;
using Arbor.HttpClient.Testing.Repositories;
using Serilog.Core;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public sealed class CollectionsManagementCoordinatorTests
{
    private const string MinimalOpenApiDocument = """
        {
          "openapi": "3.0.0",
          "info": { "title": "Test API", "version": "1.0" },
          "servers": [ { "url": "https://api.example.com" } ],
          "paths": {
            "/pets": {
              "get": {
                "operationId": "listPets",
                "responses": { "200": { "description": "ok" } }
              }
            }
          }
        }
        """;

    private sealed class Harness
    {
        public InMemoryCollectionRepository Repository { get; } = new();

        public List<Collection> Collections { get; private set; } = [];

        public Collection? SelectedCollection { get; set; }

        public CollectionsManagementCoordinator Coordinator { get; }

        public Harness()
        {
            Coordinator = new CollectionsManagementCoordinator(
                Repository,
                ReloadCollectionsAsync,
                () => Collections,
                () => SelectedCollection,
                () => new ResolvedHttpRequestDraft("Test", "GET", "https://api.example.com/pets", null),
                new OpenApiImportService(),
                Logger.None);
        }

        public async Task<Collection> SeedCollectionAsync(string name)
        {
            var collectionId = await Repository.SaveAsync(name, null, null, []);
            await ReloadCollectionsAsync(TestContext.Current.CancellationToken);
            return Collections.First(collection => collection.Id == collectionId);
        }

        public async Task ReloadCollectionsAsync(CancellationToken cancellationToken) =>
            Collections = [.. await Repository.GetAllAsync(cancellationToken)];
    }

    [Fact]
    public async Task ImportCollectionAsync_ValidOpenApiDocument_SavesCollectionAndReloads()
    {
        var harness = new Harness();

        var outcome = await harness.Coordinator.ImportCollectionAsync(
            () => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(MinimalOpenApiDocument))),
            "/tmp/test-api.json",
            TestContext.Current.CancellationToken);

        outcome.Changed.Should().BeTrue();
        outcome.ErrorMessage.Should().BeNull();
        var importedCollection = harness.Collections.Single();
        importedCollection.Id.Should().Be(outcome.SelectedCollectionId);
        importedCollection.Name.Should().Be("Test API");
        importedCollection.BaseUrl.Should().Be("https://api.example.com");
        importedCollection.Requests.Should().ContainSingle(request =>
            request.Method == "GET" && request.Path == "/pets");
    }

    [Fact]
    public async Task ImportCollectionAsync_InvalidDocument_ReturnsFailedOutcome()
    {
        var harness = new Harness();

        var outcome = await harness.Coordinator.ImportCollectionAsync(
            () => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("not an openapi document"))),
            "/tmp/garbage.json",
            TestContext.Current.CancellationToken);

        outcome.Changed.Should().BeFalse();
        outcome.ErrorMessage.Should().StartWith("Import failed:");
        harness.Collections.Should().BeEmpty();
    }

    [Fact]
    public async Task ImportCollectionAsync_StreamOpenFailure_ReturnsFailedOutcome()
    {
        var harness = new Harness();

        var outcome = await harness.Coordinator.ImportCollectionAsync(
            () => throw new IOException("disk unavailable"),
            "/tmp/test-api.json",
            TestContext.Current.CancellationToken);

        outcome.Changed.Should().BeFalse();
        outcome.ErrorMessage.Should().Be("Import failed: disk unavailable");
    }

    [Fact]
    public async Task DeleteCollectionAsync_NullCollection_ReturnsNoChange()
    {
        var harness = new Harness();
        await harness.SeedCollectionAsync("Keep me");

        var outcome = await harness.Coordinator.DeleteCollectionAsync(null, TestContext.Current.CancellationToken);

        outcome.Changed.Should().BeFalse();
        harness.Collections.Should().ContainSingle();
    }

    [Fact]
    public async Task DeleteCollectionAsync_SelectedCollection_DeletesAndReportsWasSelected()
    {
        var harness = new Harness();
        var firstCollection = await harness.SeedCollectionAsync("First");
        await harness.SeedCollectionAsync("Second");
        harness.SelectedCollection = firstCollection;

        var outcome = await harness.Coordinator.DeleteCollectionAsync(firstCollection, TestContext.Current.CancellationToken);

        outcome.Changed.Should().BeTrue();
        outcome.WasSelected.Should().BeTrue();
        harness.Collections.Should().ContainSingle().Which.Name.Should().Be("Second");
    }

    [Fact]
    public async Task DeleteCollectionAsync_UnselectedCollection_ReportsWasSelectedFalse()
    {
        var harness = new Harness();
        var firstCollection = await harness.SeedCollectionAsync("First");
        var secondCollection = await harness.SeedCollectionAsync("Second");
        harness.SelectedCollection = secondCollection;

        var outcome = await harness.Coordinator.DeleteCollectionAsync(firstCollection, TestContext.Current.CancellationToken);

        outcome.Changed.Should().BeTrue();
        outcome.WasSelected.Should().BeFalse();
        harness.Collections.Should().ContainSingle().Which.Name.Should().Be("Second");
    }
}
