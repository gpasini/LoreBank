using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LoreBank.Bank.Test.Infrastructure.Fakes;
using LoreBank.Bank.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.Bank.Test.Infrastructure.Apis;

// Hérite de BaseHostTest, pas de BaseIntegrationTest : son TransactionScope
// ambiant ne traverse pas la frontière HTTP, et une requête servie par l'hôte
// écrirait donc hors du rollback. Ces tests n'écrivent rien qui survive — le
// seul qui ouvre un compte le fait échouer.
//
// Seul endroit de la suite qui parle vraiment HTTP : les statuts, les en-têtes
// et la forme du JSON ne sont observables nulle part ailleurs.
[TestFixture]
public sealed class ErrorContractTest : BaseHostTest<BankWebAppFactory>
{
    private const string ProblemJson = "application/problem+json; charset=utf-8";

    private static readonly Guid UnknownAccountId = Guid.Empty;

    private HttpClient _client = null!;

    [SetUp]
    public void SetUp() => _client = Factory.CreateClient();

    [TearDown]
    public void TearDown() => _client.Dispose();

    [Test]
    public async Task Post_ShouldReturn422WithACode_WhenAValueObjectRefusesTheValue()
    {
        // Act

        var response = await _client.PostAsJsonAsync(
            requestUri: "api/bank/accounts",
            value: new {
                iban = "NOPE",
                currency = "EUR",
            }
        );

        // Assert

        await Expect(
            response: response,
            status: HttpStatusCode.UnprocessableEntity,
            code: "INVALID_IBAN"
        );

        (await BodyOf(response)).GetProperty("parameters").GetProperty("iban").GetString()
            .Should().Be("NOPE");
    }

    [Test]
    public async Task Get_ShouldReturn404WithACode_WhenTheAccountIsUnknown()
    {
        // Act

        var response = await _client.GetAsync($"api/bank/accounts/{UnknownAccountId}");

        // Assert

        await Expect(
            response: response,
            status: HttpStatusCode.NotFound,
            code: "BANK.BANK_ACCOUNT_NOT_FOUND"
        );

        (await BodyOf(response)).GetProperty("parameters").GetProperty("accountId").GetString()
            .Should().Be(UnknownAccountId.ToString());
    }

    [Test]
    public async Task Post_ShouldReturn404WithACode_WhenACommandTargetsAnUnknownAccount()
    {
        var response = await _client.PostAsJsonAsync(
            requestUri: $"api/bank/accounts/{UnknownAccountId}/deposits",
            value: new {
                amount = 10m,
                currency = "EUR",
            }
        );

        await Expect(
            response: response,
            status: HttpStatusCode.NotFound,
            code: "BANK.BANK_ACCOUNT_NOT_FOUND"
        );
    }

    [Test]
    public async Task Post_ShouldReturn400WithACode_WhenTheBodyDoesNotBind()
    {
        // Act

        var response = await _client.PostAsync(
            requestUri: $"api/bank/accounts/{UnknownAccountId}/deposits",
            content: new StringContent(
                content: """{"amount":"beaucoup","currency":"EUR"}""",
                encoding: Encoding.UTF8,
                mediaType: "application/json"
            )
        );

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
        var response = await _client.PostAsync(
            requestUri: $"api/bank/accounts/{UnknownAccountId}/deposits",
            content: new StringContent(
                content: """{"amount":"beaucoup","currency":"EUR"}""",
                encoding: Encoding.UTF8,
                mediaType: "application/json"
            )
        );

        (await response.Content.ReadAsStringAsync()).Should().NotContain("LoreBank.Bank.Api.Contracts");
    }

    [Test]
    public async Task Post_ShouldReturn500WithoutACode_WhenANonDomainExceptionEscapes()
    {
        // Arrange

        WelcomeLetterSender.ThrowOnSend = new InvalidOperationException("boom");

        // Act

        var response = await _client.PostAsJsonAsync(
            requestUri: "api/bank/accounts",
            value: new {
                iban = "IT60X0542811101000000123456",
                currency = "EUR",
            }
        );

        // Assert

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.ToString().Should().Be(ProblemJson);

        var body = await BodyOf(response);

        body.GetProperty("title").GetString().Should().Be("Internal Server Error");
        body.TryGetProperty(
            propertyName: "code",
            value: out _
        ).Should().BeFalse();
    }

    // Le message de l'exception ne doit jamais atteindre le client, quel que
    // soit l'environnement — l'hôte de test tourne en Development.
    [Test]
    public async Task Post_ShouldLeakNoStackTrace_WhenANonDomainExceptionEscapes()
    {
        WelcomeLetterSender.ThrowOnSend = new InvalidOperationException("boom");

        var response = await _client.PostAsJsonAsync(
            requestUri: "api/bank/accounts",
            value: new {
                iban = "IT60X0542811101000000123456",
                currency = "EUR",
            }
        );

        (await response.Content.ReadAsStringAsync()).Should().NotContain("boom");
    }

    private static ConfigurableWelcomeLetterSender WelcomeLetterSender =>
        Factory.Services.GetRequiredService<ConfigurableWelcomeLetterSender>();

    private static async Task Expect(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code
    )
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.ToString().Should().Be(ProblemJson);

        var body = await BodyOf(response);

        body.GetProperty("code").GetString().Should().Be(code);
        body.TryGetProperty(
            propertyName: "detail",
            value: out _
        ).Should().BeFalse();
    }

    private static async Task<JsonElement> BodyOf(HttpResponseMessage response) => JsonDocument
        .Parse(await response.Content.ReadAsStringAsync())
        .RootElement;
}
