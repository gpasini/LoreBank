using System.Net;
using System.Text.Json;
using LoreBank.SharedKernel.Api.OpenApi;
using LoreBank.SharedKernel.Api.Problems;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.SharedKernel.Test.Infrastructure.Apis;

// La Description OpenAPI dit ce que ModuleController fait (ADR 0019) : sa
// preuve vit ici, sur le ProbeController — un module d'exemple supprimable
// n'est pas un ancrage. Le document est lu par l'endpoint réel de l'hôte de
// test (Development), la même génération que l'émission au build.
[TestFixture]
public sealed class DescriptionContractTest : BaseHostTest<SharedKernelWebAppFactory>
{
    private const string Problem = "application/problem+json";

    private static JsonElement _document;

    [OneTimeSetUp]
    public async Task LoadDocument()
    {
        using var client = Factory.CreateClient();
        using var response = await client.GetAsync("openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        _document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    [Test]
    public void Command_ShouldBeDescribedAs204WithoutContent_WhenTheActionReturnsCommandResult()
    {
        var responses = Operation(
            path: "/api/probe/commands",
            verb: "post"
        ).GetProperty("responses");

        responses.TryGetProperty(
            propertyName: "200",
            value: out _
        ).Should().BeFalse("l'ApiExplorer inférerait 200 d'un ActionResult nu");
        responses.GetProperty("204").TryGetProperty(
            propertyName: "content",
            value: out _
        ).Should().BeFalse();
    }

    [Test]
    public void Creation_ShouldBeDescribedAs201WithLocationAndWithoutContent_WhenTheActionReturnsCreationResult()
    {
        var responses = Operation(
            path: "/api/probe/creations",
            verb: "post"
        ).GetProperty("responses");

        responses.TryGetProperty(
            propertyName: "200",
            value: out _
        ).Should().BeFalse();

        var created = responses.GetProperty("201");

        created.TryGetProperty(
            propertyName: "content",
            value: out _
        ).Should().BeFalse();
        created.GetProperty("headers").GetProperty("Location").GetProperty("required").GetBoolean().Should().BeTrue();
    }

    [Test]
    public void Reading_ShouldBeDescribedAs200WithItsResult_WhenTheActionReturnsAnActionResultOfT()
    {
        var ok = Operation(
            path: "/api/probe/readings/{id}",
            verb: "get"
        ).GetProperty("responses").GetProperty("200");

        ok.GetProperty("content").EnumerateObject().Select(media => media.Name)
            .Should().Equal("application/json");
        ok.GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString()
            .Should().Be("#/components/schemas/ProbeResult");
    }

    [Test]
    public void Stream_ShouldBeDescribedAs200EventStreamOnSignal_WhenTheActionReturnsSignalStreamResult()
    {
        // Le flux de Signaux (ADR 0026) : la seule opération en
        // text/event-stream, sur le schéma Signal — le Client en tire le type
        // du message et le chemin.
        var operation = Operation(
            path: "/api/signals",
            verb: "get"
        );

        operation.GetProperty("operationId").GetString().Should().Be("Signals_Subscribe");
        operation.GetProperty("parameters").EnumerateArray().Select(parameter => parameter.GetProperty("name").GetString())
            .Should().Equal("resource");

        var ok = operation.GetProperty("responses").GetProperty("200");

        ok.GetProperty("content").EnumerateObject().Select(media => media.Name)
            .Should().Equal("text/event-stream");
        ok.GetProperty("content").GetProperty("text/event-stream").GetProperty("schema").GetProperty("$ref").GetString()
            .Should().Be("#/components/schemas/Signal");

        var signal = Schema("Signal");

        signal.GetProperty("properties").EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(
                "discriminant",
                "resourceKind",
                "resourceId",
                "occurredAt"
            );
        signal.GetProperty("required").EnumerateArray().Select(field => field.GetString())
            .Should().BeEquivalentTo(
                "discriminant",
                "resourceKind",
                "resourceId",
                "occurredAt"
            );
    }

    // La Liste (ADR 0027) : la query se lit paramètre par paramètre sur la
    // query string, en camelCase comme les clés JSON — page, pageSize et
    // search du socle, les filtres de la query en tableaux — et la réponse est
    // la Page de son item, sous le nom que le générateur dérive du générique.
    [Test]
    public void List_ShouldBeDescribedWithCamelCaseQueryParameters_AndAPageOfItsItem()
    {
        var operation = Operation(
            path: "/api/probe/listings",
            verb: "get"
        );

        var parameters = operation.GetProperty("parameters").EnumerateArray()
            .ToDictionary(
                keySelector: parameter => parameter.GetProperty("name").GetString()!,
                elementSelector: parameter => parameter
            );

        parameters.Keys.Should().BeEquivalentTo(
            "page",
            "pageSize",
            "search",
            "kind",
            "active"
        );
        parameters.Values.Select(parameter => parameter.GetProperty("in").GetString()).Should().AllBe("query");
        parameters["page"].GetProperty("schema").GetProperty("type").GetString().Should().Be("integer");
        parameters["page"].GetProperty("schema").TryGetProperty(
            propertyName: "pattern",
            value: out _
        ).Should().BeFalse();
        parameters["kind"].GetProperty("schema").GetProperty("type").GetString().Should().Be("array");
        parameters["kind"].GetProperty("schema").GetProperty("items").GetProperty("type").GetString().Should().Be("string");
        parameters["active"].GetProperty("schema").GetProperty("items").GetProperty("type").GetString().Should().Be("boolean");

        operation.GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json")
            .GetProperty("schema").GetProperty("$ref").GetString()
            .Should().Be("#/components/schemas/ListPageOfProbeThingResult");

        var page = Schema("ListPageOfProbeThingResult");

        page.GetProperty("required").EnumerateArray().Select(field => field.GetString()).Should().Equal(
            "items",
            "page",
            "pageSize",
            "totalCount",
            "facets"
        );
        page.GetProperty("properties").GetProperty("items").GetProperty("items").GetProperty("$ref").GetString()
            .Should().Be("#/components/schemas/ProbeThingResult");
        page.GetProperty("properties").GetProperty("totalCount").GetProperty("type").GetString().Should().Be("integer");
        page.GetProperty("properties").GetProperty("facets").GetProperty("items").GetProperty("$ref").GetString()
            .Should().Be("#/components/schemas/Facet");

        Schema("Facet").GetProperty("required").EnumerateArray().Select(field => field.GetString()).Should().Equal(
            "name",
            "values"
        );
        Schema("FacetValue").GetProperty("required").EnumerateArray().Select(field => field.GetString()).Should().Equal(
            "value",
            "count"
        );
    }

    [Test]
    public void EveryOperation_ShouldDeclareTheCommonErrorsOnApiProblem_WhateverItsShape()
    {
        foreach (var operation in Operations()) {
            var responses = operation.GetProperty("responses");

            foreach (var status in new[] { "400", "404", "409", "422", "500" }) {
                var content = responses.GetProperty(status).GetProperty("content");

                content.EnumerateObject().Select(media => media.Name).Should().Equal(Problem);
                content.GetProperty(Problem).GetProperty("schema").GetProperty("$ref").GetString()
                    .Should().Be($"#/components/schemas/{DescriptionDocumentTransformer.ProblemSchemaName}");
            }
        }
    }

    [Test]
    public void ApiProblem_ShouldHaveTheShapeOfTheErrorContract_WithItsCodeTypedByErrorCode()
    {
        var problem = Schema(DescriptionDocumentTransformer.ProblemSchemaName);

        problem.GetProperty("required").EnumerateArray().Select(field => field.GetString())
            .Should().BeEquivalentTo(
                "title",
                "status",
                "traceId"
            );
        problem.GetProperty("properties").EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(
                "title",
                "status",
                "code",
                "parameters",
                "traceId"
            );
        problem.GetProperty("properties").GetProperty("code").GetProperty("$ref").GetString()
            .Should().Be($"#/components/schemas/{ErrorCodes.SchemaName}");
        problem.TryGetProperty(
            propertyName: "detail",
            value: out _
        ).Should().BeFalse();
    }

    [Test]
    public void ErrorCode_ShouldBeAStringEnum_ThatIncludesTheBindingCode()
    {
        var errorCode = Schema(ErrorCodes.SchemaName);

        errorCode.GetProperty("type").GetString().Should().Be("string");
        errorCode.GetProperty("enum").EnumerateArray().Select(code => code.GetString())
            .Should().Contain("VALIDATION_FAILED");
    }

    [Test]
    public void OperationId_ShouldBeControllerAndAction_ForEveryOperation()
    {
        Operation(
            path: "/api/probe/commands",
            verb: "post"
        ).GetProperty("operationId").GetString().Should().Be("Probe_Command");
        Operation(
            path: "/api/probe/readings/{id}",
            verb: "get"
        ).GetProperty("operationId").GetString().Should().Be("Probe_Read");
    }

    [Test]
    public void Tag_ShouldBeTheModuleOfTheController_ReadOnItsAssembly()
    {
        Operation(
            path: "/api/probe/commands",
            verb: "post"
        ).GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()).Should().Equal("SharedKernel");
    }

    [Test]
    public void RequestBody_ShouldOfferJsonOnly_WhenTheActionBindsACommand()
    {
        Operation(
            path: "/api/probe/commands",
            verb: "post"
        ).GetProperty("requestBody").GetProperty("content").EnumerateObject().Select(media => media.Name)
            .Should().Equal("application/json");
    }

    [Test]
    public void Decimal_ShouldBeANumberWithoutPattern_WhereverItAppears()
    {
        var amount = Schema("ProbeResult").GetProperty("properties").GetProperty("amount");

        amount.GetProperty("type").GetString().Should().Be("number");
        amount.TryGetProperty(
            propertyName: "pattern",
            value: out _
        ).Should().BeFalse();
    }

    [Test]
    public void RouteBoundProperty_ShouldBeAbsentFromTheBody_WhenTheRouteOverridesIt()
    {
        var body = Schema("ProbeMixedCommand");

        body.GetProperty("properties").EnumerateObject().Select(property => property.Name).Should().Equal("amount");
        body.GetProperty("required").EnumerateArray().Select(field => field.GetString()).Should().Equal("amount");
    }

    [Test]
    public void Document_ShouldDescribeASurfaceAndNotADeployment()
    {
        _document.GetProperty("info").GetProperty("title").GetString().Should().Be("LoreBank");
        _document.TryGetProperty(
            propertyName: "servers",
            value: out _
        ).Should().BeFalse();
    }

    [Test]
    public void ErrorResponses_ShouldUseTheProblemMediaType_ThatApiProblemActuallyServes() =>
        Problem.Should().Be(ApiProblem.ContentType);

    private static JsonElement Operation(
        string path,
        string verb
    ) => _document.GetProperty("paths").GetProperty(path).GetProperty(verb);

    private static IEnumerable<JsonElement> Operations() => _document.GetProperty("paths").EnumerateObject()
        .SelectMany(path => path.Value.EnumerateObject().Select(verb => verb.Value));

    private static JsonElement Schema(string name) => _document.GetProperty("components").GetProperty("schemas").GetProperty(name);
}
