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
                "status"
            );
        problem.GetProperty("properties").EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(
                "title",
                "status",
                "code",
                "parameters"
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
