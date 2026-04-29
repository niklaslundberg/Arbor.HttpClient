using System.Text;
using Arbor.HttpClient.Core.OpenApiImport;

namespace Arbor.HttpClient.Core.Tests;

public class OpenApiImportServiceTests
{
    private readonly OpenApiImportService _service = new();

    private const string PetstoreV3 = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Pet Store", "version": "1.0.0" },
          "servers": [{ "url": "http://localhost:5000/v1" }],
          "paths": {
            "/pets": {
              "get": { "operationId": "listPets", "summary": "List all pets" },
              "post": { "operationId": "createPet", "summary": "Create a pet" }
            },
            "/pets/{petId}": {
              "get": { "operationId": "showPetById", "summary": "Info for a specific pet" }
            }
          }
        }
        """;

    private const string PetstoreWithExtras = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Pet Store Extended", "version": "1.0.0" },
          "servers": [{ "url": "http://localhost:5000/v1" }],
          "components": {
            "securitySchemes": {
              "bearerAuth": { "type": "http", "scheme": "bearer" },
              "apiKeyAuth": { "type": "apiKey", "in": "header", "name": "X-API-Key" }
            }
          },
          "paths": {
            "/pets": {
              "get": {
                "operationId": "listPets",
                "summary": "List all pets",
                "tags": ["pets"],
                "parameters": [
                  { "name": "limit", "in": "query", "schema": { "type": "integer" } },
                  { "name": "offset", "in": "query", "schema": { "type": "integer" } },
                  { "name": "X-Request-ID", "in": "header", "schema": { "type": "string" } }
                ],
                "security": [{ "bearerAuth": [] }]
              },
              "post": {
                "operationId": "createPet",
                "summary": "Create a pet",
                "tags": ["pets"],
                "security": [{ "apiKeyAuth": [] }],
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": {
                      "example": { "name": "Fluffy", "tag": "cat" }
                    }
                  }
                }
              }
            },
            "/orders": {
              "post": {
                "operationId": "createOrder",
                "summary": "Place an order",
                "tags": ["orders"],
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": {
                      "examples": {
                        "sample": { "value": { "item": "widget", "qty": 1 } }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;

    [Fact]
    public void Import_ShouldParseCollectionName()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(PetstoreV3));
        var collection = _service.Import(stream);
        collection.Name.Should().Be("Pet Store");
    }

    [Fact]
    public void Import_ShouldParseBaseUrl()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(PetstoreV3));
        var collection = _service.Import(stream);
        collection.BaseUrl.Should().Be("http://localhost:5000/v1");
    }

    [Fact]
    public void Import_ShouldParseAllEndpoints()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(PetstoreV3));
        var collection = _service.Import(stream);
        collection.Requests.Should().HaveCount(3);
    }

    [Fact]
    public void Import_ShouldMapOperationIdAsName()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(PetstoreV3));
        var collection = _service.Import(stream);
        collection.Requests.Should().Contain(r => r.Name == "listPets" && r.Method == "GET");
        collection.Requests.Should().Contain(r => r.Name == "createPet" && r.Method == "POST");
    }

    [Fact]
    public void Import_ShouldConvertPathParamsToDoublebraceFormat()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(PetstoreV3));
        var collection = _service.Import(stream);
        collection.Requests.Should().Contain(r => r.Path == "/pets/{{petId}}");
    }

    [Fact]
    public void Import_ShouldThrowForSwagger2()
    {
        const string swagger2 = """
            {
              "swagger": "2.0",
              "info": { "title": "Old API", "version": "1.0" },
              "paths": {}
            }
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(swagger2));
        var act = () => _service.Import(stream);
        act.Should().Throw<Exception>();
    }

    // ── Tag-based grouping ────────────────────────────────────────────────────

    [Fact]
    public void Import_ShouldSetTagFromFirstOperationTag()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(PetstoreWithExtras));
        var collection = _service.Import(stream);
        collection.Requests.Should().Contain(r => r.Name == "listPets" && r.Tag == "pets");
        collection.Requests.Should().Contain(r => r.Name == "createOrder" && r.Tag == "orders");
    }

    [Fact]
    public void Import_ShouldLeaveTagNullWhenNoTagsDefined()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(PetstoreV3));
        var collection = _service.Import(stream);
        collection.Requests.Should().AllSatisfy(r => r.Tag.Should().BeNull());
    }

    // ── Query parameters ──────────────────────────────────────────────────────

    [Fact]
    public void Import_ShouldAppendQueryParamsToPath()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(PetstoreWithExtras));
        var collection = _service.Import(stream);
        var listPets = collection.Requests.Single(r => r.Name == "listPets");
        listPets.Path.Should().Be("/pets?limit={{limit}}&offset={{offset}}");
    }

    [Fact]
    public void Import_ShouldNotAppendQueryStringWhenNoQueryParams()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(PetstoreV3));
        var collection = _service.Import(stream);
        collection.Requests.Should().AllSatisfy(r => r.Path.Should().NotContain("?"));
    }

    // ── Header parameters ─────────────────────────────────────────────────────

    [Fact]
    public void Import_ShouldAddHeaderParamAsRequestHeader()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(PetstoreWithExtras));
        var collection = _service.Import(stream);
        var listPets = collection.Requests.Single(r => r.Name == "listPets");
        listPets.Headers.Should().ContainSingle(h => h.Name == "X-Request-ID" && h.Value == "{{X-Request-ID}}");
    }

    // ── Auth headers ──────────────────────────────────────────────────────────

    [Fact]
    public void Import_ShouldAddBearerAuthHeaderFromSecurityScheme()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(PetstoreWithExtras));
        var collection = _service.Import(stream);
        var listPets = collection.Requests.Single(r => r.Name == "listPets");
        listPets.Headers.Should().ContainSingle(h =>
            h.Name == "Authorization" && h.Value == "Bearer {{bearerToken}}");
    }

    [Fact]
    public void Import_ShouldAddApiKeyHeaderFromSecurityScheme()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(PetstoreWithExtras));
        var collection = _service.Import(stream);
        var createPet = collection.Requests.Single(r => r.Name == "createPet");
        createPet.Headers.Should().ContainSingle(h =>
            h.Name == "X-API-Key" && h.Value == "{{X-API-Key}}");
    }

    [Fact]
    public void Import_ShouldLeaveHeadersNullWhenNonePresent()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(PetstoreV3));
        var collection = _service.Import(stream);
        collection.Requests.Should().AllSatisfy(r => r.Headers.Should().BeNull());
    }

    // ── Body from example ─────────────────────────────────────────────────────

    [Fact]
    public void Import_ShouldSetBodyFromInlineExample()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(PetstoreWithExtras));
        var collection = _service.Import(stream);
        var createPet = collection.Requests.Single(r => r.Name == "createPet");
        createPet.Body.Should().NotBeNullOrEmpty();
        createPet.Body.Should().Contain("Fluffy");
    }

    [Fact]
    public void Import_ShouldSetBodyFromNamedExample()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(PetstoreWithExtras));
        var collection = _service.Import(stream);
        var createOrder = collection.Requests.Single(r => r.Name == "createOrder");
        createOrder.Body.Should().NotBeNullOrEmpty();
        createOrder.Body.Should().Contain("widget");
    }

    [Fact]
    public void Import_ShouldLeaveBodyNullWhenNoRequestBody()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(PetstoreV3));
        var collection = _service.Import(stream);
        collection.Requests.Should().AllSatisfy(r => r.Body.Should().BeNull());
    }

    // ── Content type ──────────────────────────────────────────────────────────

    [Fact]
    public void Import_ShouldSetContentTypeFromRequestBody()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(PetstoreWithExtras));
        var collection = _service.Import(stream);
        var createPet = collection.Requests.Single(r => r.Name == "createPet");
        createPet.ContentType.Should().Be("application/json");
    }

    [Fact]
    public void Import_ShouldLeaveContentTypeNullWhenNoRequestBody()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(PetstoreV3));
        var collection = _service.Import(stream);
        collection.Requests.Should().AllSatisfy(r => r.ContentType.Should().BeNull());
    }
}
