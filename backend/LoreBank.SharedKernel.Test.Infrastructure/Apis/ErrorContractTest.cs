using System.Net;
using System.Text;
using System.Text.Json;
using LoreBank.SharedKernel.Api.Problems;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.SharedKernel.Test.Infrastructure.Apis;

// Le contrat d'erreur (statuts, type de média, code, absence de detail) est la
// plomberie de SharedKernel.Api : sa preuve vit ici, pas dans un module métier
// qu'un cloneur du template supprimera. Le déclencheur est le ProbeController,
// monté par SharedKernelWebAppFactory seulement — un throw direct suffit,
// DomainExceptionFilter voit la même chose qu'en sortie de handler.
//
// Hérite de BaseHostTest, pas de BaseIntegrationTest : le TransactionScope
// ambiant ne traverse pas la frontière HTTP. Seul endroit de la suite, avec
// CqsContractTest côté Bank, qui parle vraiment HTTP.
[TestFixture]
public sealed class ErrorContractTest : BaseHostTest<SharedKernelWebAppFactory>
{
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp() => _client = Factory.CreateClient();

    [TearDown]
    public void TearDown() => _client.Dispose();

    [Test]
    public async Task Get_ShouldReturn422WithACode_WhenADomainExceptionEscapes()
    {
        // Act

        var response = await _client.GetAsync("api/probe/domain-failure");

        // Assert

        await Expect(
            response: response,
            status: HttpStatusCode.UnprocessableEntity,
            code: "PROBE_FAILURE"
        );

        (await BodyOf(response)).GetProperty("parameters").GetProperty("reason").GetString()
            .Should().Be("NOPE");
    }

    [Test]
    public async Task Get_ShouldReturn404WithACode_WhenTheThingIsMissing()
    {
        // Arrange

        var thingId = Guid.NewGuid();

        // Act

        var response = await _client.GetAsync($"api/probe/things/{thingId}");

        // Assert

        await Expect(
            response: response,
            status: HttpStatusCode.NotFound,
            code: "PROBE_THING_NOT_FOUND"
        );

        (await BodyOf(response)).GetProperty("parameters").GetProperty("thingId").GetString()
            .Should().Be(thingId.ToString());
    }

    [Test]
    public async Task Post_ShouldReturn409WithACode_WhenTheAggregateVersionIsStale()
    {
        // Arrange

        var thingId = Guid.NewGuid();

        // Act

        var response = await _client.PostAsync(
            requestUri: $"api/probe/things/{thingId}/concurrent-updates",
            content: null
        );

        // Assert

        await Expect(
            response: response,
            status: HttpStatusCode.Conflict,
            code: "CONCURRENT_UPDATE"
        );

        (await BodyOf(response)).GetProperty("parameters").GetProperty("id").GetString()
            .Should().Be(thingId.ToString());
    }

    [Test]
    public async Task Post_ShouldReturn400WithACode_WhenTheBodyDoesNotBind()
    {
        // Act

        var response = await PostUnbindableBodyAsync();

        // Assert

        await Expect(
            response: response,
            status: HttpStatusCode.BadRequest,
            code: "VALIDATION_FAILED"
        );
    }

    [Test]
    public async Task Post_ShouldNotNameTheFaultyDotNetType_WhenTheBodyDoesNotBind()
    {
        var response = await PostUnbindableBodyAsync();

        (await response.Content.ReadAsStringAsync())
            .Should().NotContain("LoreBank.SharedKernel.Test.Infrastructure");
    }

    [Test]
    public async Task Get_ShouldReturn500WithoutACode_WhenANonDomainExceptionEscapes()
    {
        // Act

        var response = await _client.GetAsync("api/probe/crash");

        // Assert

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.ToString().Should().Be(ApiProblem.ContentTypeWithCharset);

        var body = await BodyOf(response);

        body.GetProperty("title").GetString().Should().Be("Internal Server Error");
        body.TryGetProperty(
            propertyName: "code",
            value: out _
        ).Should().BeFalse(); ExpectCorrelation(body);
    }

    // Un traceparent entrant est honoré : la Corrélation est celle de
    // l'appelant, pas une nouvelle — le comportement du framework, épinglé.
    [Test]
    public async Task Get_ShouldEchoTheCallersTraceId_WhenATraceparentIsSent()
    {
        // Arrange

        const string traceId = "0af7651916cd43dd8448eb211c80319c";

        using var request = new HttpRequestMessage(
            method: HttpMethod.Get,
            requestUri: "api/probe/domain-failure"
        );
        request.Headers.Add(
            name: "traceparent",
            value: $"00-{traceId}-b7ad6b7169203331-01"
        );

        // Act

        var response = await _client.SendAsync(request);

        // Assert

        (await BodyOf(response)).GetProperty("traceId").GetString().Should().Be(traceId);
    }

    // Le message de l'exception ne doit jamais atteindre le client, quel que
    // soit l'environnement — l'hôte de test tourne en Development.
    [Test]
    public async Task Get_ShouldLeakNoStackTrace_WhenANonDomainExceptionEscapes()
    {
        var response = await _client.GetAsync("api/probe/crash");

        (await response.Content.ReadAsStringAsync()).Should().NotContain("boom");
    }

    private Task<HttpResponseMessage> PostUnbindableBodyAsync() => _client.PostAsync(
        requestUri: "api/probe/bindings",
        content: new StringContent(
            content: """{"amount":"beaucoup"}""",
            encoding: Encoding.UTF8,
            mediaType: "application/json"
        )
    );

    private static async Task Expect(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code
    )
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.ToString().Should().Be(ApiProblem.ContentTypeWithCharset);

        var body = await BodyOf(response);

        body.GetProperty("code").GetString().Should().Be(code);
        body.TryGetProperty(
            propertyName: "detail",
            value: out _
        ).Should().BeFalse();
        ExpectCorrelation(body);
    }

    // La Corrélation (ADR 0022) : l'identifiant de trace W3C, 32 hexadécimaux,
    // sur toute erreur — c'est ce que le client cite quand il ouvre un ticket.
    private static void ExpectCorrelation(JsonElement body) =>
        body.GetProperty("traceId").GetString().Should().MatchRegex("^[0-9a-f]{32}$");

    private static async Task<JsonElement> BodyOf(HttpResponseMessage response) => JsonDocument
        .Parse(await response.Content.ReadAsStringAsync())
        .RootElement;
}
