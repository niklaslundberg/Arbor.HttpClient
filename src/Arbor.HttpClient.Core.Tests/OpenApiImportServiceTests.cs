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

    [Fact]
    public void Import_ShouldLeaveBodyNullWhenRequestBodyHasNoExample()
    {
        const string specWithNoExample = """
            {
              "openapi": "3.0.3",
              "info": { "title": "Test API", "version": "1.0.0" },
              "paths": {
                "/items": {
                  "post": {
                    "operationId": "createItem",
                    "requestBody": {
                      "required": true,
                      "content": {
                        "application/json": {
                          "schema": {
                            "type": "object",
                            "properties": {
                              "name": { "type": "string" }
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(specWithNoExample));
        var collection = _service.Import(stream);
        var createItem = collection.Requests.Single(r => r.Name == "createItem");
        createItem.Body.Should().BeNull();
        createItem.ContentType.Should().Be("application/json");
    }

    [Fact]
    public void Import_ShouldLeaveBodyNullWhenMediaTypeValueIsNull()
    {
        // The OpenAPI parser sets a null OpenApiMediaType value in Content when the
        // content entry is explicitly "null" in the document. This would previously
        // throw NullReferenceException when accessing mediaType.Example.
        const string specWithNullMediaType = """
            {
              "openapi": "3.0.3",
              "info": { "title": "Test API", "version": "1.0.0" },
              "paths": {
                "/items": {
                  "post": {
                    "operationId": "createItem",
                    "requestBody": {
                      "required": true,
                      "content": {
                        "application/json": null
                      }
                    }
                  }
                }
              }
            }
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(specWithNullMediaType));
        var collection = _service.Import(stream);
        var createItem = collection.Requests.Single(r => r.Name == "createItem");
        createItem.Body.Should().BeNull();
        createItem.ContentType.Should().Be("application/json");
    }

    [Fact]
    public void Import_ShouldLeaveBodyNullWhenNamedExampleValueIsNull()
    {
        // The OpenAPI parser sets a null OpenApiExample entry in the Examples dictionary
        // when an example is explicitly "null" in the document. This would previously
        // throw NullReferenceException when calling .First().Value on that entry.
        const string specWithNullNamedExample = """
            {
              "openapi": "3.0.3",
              "info": { "title": "Test API", "version": "1.0.0" },
              "paths": {
                "/items": {
                  "post": {
                    "operationId": "createItem",
                    "requestBody": {
                      "required": true,
                      "content": {
                        "application/json": {
                          "examples": {
                            "sample": null
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(specWithNullNamedExample));
        var collection = _service.Import(stream);
        var createItem = collection.Requests.Single(r => r.Name == "createItem");
        createItem.Body.Should().BeNull();
        createItem.ContentType.Should().Be("application/json");
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

    // ── Path-item-level parameters ────────────────────────────────────────────

    [Fact]
    public void Import_ShouldMergePathItemQueryParamsWithOperationParams()
    {
        const string specWithPathItemParams = """
            {
              "openapi": "3.0.3",
              "info": { "title": "Test API", "version": "1.0.0" },
              "paths": {
                "/items": {
                  "parameters": [
                    { "name": "apiVersion", "in": "query", "schema": { "type": "string" } }
                  ],
                  "get": {
                    "operationId": "listItems",
                    "parameters": [
                      { "name": "limit", "in": "query", "schema": { "type": "integer" } }
                    ]
                  }
                }
              }
            }
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(specWithPathItemParams));
        var collection = _service.Import(stream);
        var listItems = collection.Requests.Single(r => r.Name == "listItems");
        listItems.Path.Should().Contain("apiVersion={{apiVersion}}");
        listItems.Path.Should().Contain("limit={{limit}}");
    }

    [Fact]
    public void Import_ShouldLetOperationParamOverridePathItemParam()
    {
        const string specWithOverride = """
            {
              "openapi": "3.0.3",
              "info": { "title": "Test API", "version": "1.0.0" },
              "paths": {
                "/items": {
                  "parameters": [
                    { "name": "format", "in": "query", "schema": { "type": "string" }, "description": "path-item" }
                  ],
                  "get": {
                    "operationId": "listItems",
                    "parameters": [
                      { "name": "format", "in": "query", "schema": { "type": "string" }, "description": "operation" }
                    ]
                  }
                }
              }
            }
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(specWithOverride));
        var collection = _service.Import(stream);
        var listItems = collection.Requests.Single(r => r.Name == "listItems");
        // Should appear exactly once (not duplicated)
        listItems.Path.Should().Contain("format={{format}}");
        listItems.Path.Split("format=").Length.Should().Be(2);
    }

    // ── Document-level security fallback ─────────────────────────────────────

    [Fact]
    public void Import_ShouldUseDocumentSecurityWhenOperationSecurityIsNull()
    {
        const string specWithGlobalSecurity = """
            {
              "openapi": "3.0.3",
              "info": { "title": "Secured API", "version": "1.0.0" },
              "security": [{ "globalBearer": [] }],
              "components": {
                "securitySchemes": {
                  "globalBearer": { "type": "http", "scheme": "bearer" }
                }
              },
              "paths": {
                "/data": {
                  "get": {
                    "operationId": "getData",
                    "summary": "Get data"
                  }
                }
              }
            }
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(specWithGlobalSecurity));
        var collection = _service.Import(stream);
        var getData = collection.Requests.Single(r => r.Name == "getData");
        getData.Headers.Should().ContainSingle(h =>
            h.Name == "Authorization" && h.Value == "Bearer {{bearerToken}}");
    }

    // ── Collection name fallbacks ─────────────────────────────────────────────

    [Fact]
    public void Import_WhenDocumentHasNoTitle_ShouldUseFallbackFromSourcePath()
    {
        const string specWithNoTitle = """
            {
              "openapi": "3.0.3",
              "info": { "version": "1.0.0" },
              "paths": {}
            }
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(specWithNoTitle));
        var collection = _service.Import(stream, "/path/to/my-api.json");
        collection.Name.Should().Be("my-api");
    }

    [Fact]
    public void Import_WhenDocumentHasNoTitleAndNoSourcePath_ShouldUseDefaultName()
    {
        const string specWithNoTitle = """
            {
              "openapi": "3.0.3",
              "info": { "version": "1.0.0" },
              "paths": {}
            }
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(specWithNoTitle));
        var collection = _service.Import(stream, sourcePath: null);
        collection.Name.Should().Be("Imported Collection");
    }

    // ── No servers ────────────────────────────────────────────────────────────

    [Fact]
    public void Import_WhenDocumentHasNoServers_ShouldReturnNullBaseUrl()
    {
        const string specWithNoServers = """
            {
              "openapi": "3.0.3",
              "info": { "title": "No Server API", "version": "1.0.0" },
              "paths": {
                "/items": {
                  "get": { "operationId": "listItems" }
                }
              }
            }
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(specWithNoServers));
        var collection = _service.Import(stream);
        collection.BaseUrl.Should().BeNull();
    }

    // ── Operation name fallback when no operationId ───────────────────────────

    [Fact]
    public void Import_WhenOperationHasNoOperationId_ShouldUseMethodAndPathAsFallbackName()
    {
        const string specWithNoOperationId = """
            {
              "openapi": "3.0.3",
              "info": { "title": "Test API", "version": "1.0.0" },
              "paths": {
                "/items/{id}": {
                  "get": {
                    "summary": "Get item by id"
                  }
                }
              }
            }
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(specWithNoOperationId));
        var collection = _service.Import(stream);
        collection.Requests.Should().ContainSingle();
        collection.Requests[0].Name.Should().Be("GET /items/{id}");
    }

    // ── Basic auth security scheme ────────────────────────────────────────────

    [Fact]
    public void Import_ShouldAddBasicAuthHeaderFromSecurityScheme()
    {
        const string specWithBasicAuth = """
            {
              "openapi": "3.0.3",
              "info": { "title": "Basic Auth API", "version": "1.0.0" },
              "components": {
                "securitySchemes": {
                  "basicAuth": { "type": "http", "scheme": "basic" }
                }
              },
              "paths": {
                "/data": {
                  "get": {
                    "operationId": "getData",
                    "security": [{ "basicAuth": [] }]
                  }
                }
              }
            }
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(specWithBasicAuth));
        var collection = _service.Import(stream);
        var getData = collection.Requests.Single(r => r.Name == "getData");
        getData.Headers.Should().ContainSingle(h =>
            h.Name == "Authorization" && h.Value == "Basic {{credentials}}");
    }

    // ── ConvertAny primitive type coverage ────────────────────────────────────

    [Fact]
    public void Import_ShouldSerializeOpenApiExampleWithAllPrimitiveTypes()
    {
        const string specWithPrimitiveExamples = """
            {
              "openapi": "3.0.3",
              "info": { "title": "Primitive Types API", "version": "1.0.0" },
              "paths": {
                "/create": {
                  "post": {
                    "operationId": "create",
                    "requestBody": {
                      "required": true,
                      "content": {
                        "application/json": {
                          "example": {
                            "name": "test",
                            "count": 42,
                            "ratio": 3.14,
                            "active": true
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(specWithPrimitiveExamples));
        var collection = _service.Import(stream);
        var create = collection.Requests.Single(r => r.Name == "create");
        create.Body.Should().NotBeNullOrEmpty();
        create.Body.Should().Contain("test");
        create.Body.Should().Contain("42");
        create.Body.Should().Contain("true");
        create.Body.Should().Contain("3.14");
    }

    [Fact]
    public void Import_ShouldHandleNonJsonContentTypeFallback()
    {
        const string specWithFormContent = """
            {
              "openapi": "3.0.3",
              "info": { "title": "Form API", "version": "1.0.0" },
              "paths": {
                "/upload": {
                  "post": {
                    "operationId": "upload",
                    "requestBody": {
                      "required": true,
                      "content": {
                        "text/plain": {
                          "example": "hello world"
                        }
                      }
                    }
                  }
                }
              }
            }
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(specWithFormContent));
        var collection = _service.Import(stream);
        var upload = collection.Requests.Single(r => r.Name == "upload");
        upload.ContentType.Should().Be("text/plain");
        upload.Body.Should().NotBeNullOrEmpty();
        upload.Body.Should().Contain("hello world");
    }
}
