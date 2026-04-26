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
}
