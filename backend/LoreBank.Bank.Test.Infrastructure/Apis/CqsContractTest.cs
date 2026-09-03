using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoreBank.Bank.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.Bank.Test.Infrastructure.Apis;

// Hérite de BaseHostTest, pour la même raison qu'ErrorContractTest :
// le TransactionScope ambiant ne traverse pas la frontière HTTP. Ces tests
// écrivent donc pour de vrai, d'où des IBAN qui leur sont propres.
//
// Épingle le contrat CQS au bord HTTP : une commande ne sert aucune
// représentation, une lecture en sert une.
[TestFixture]
public sealed class CqsContractTest : BaseHostTest<BankWebAppFactory>
{
    private const string OpeningIban = "PT50000201231234567890154";

    private const string DepositIban = "BE68539007547034";

    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _client = Factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
    }

    [Test]
    public async Task Post_ShouldReturn201WithALocationAndNoBody_WhenAnAccountIsOpened()
    {
        // Act

        var response = await OpenAsync(OpeningIban);

        // Assert

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    // La commande ne renvoie rien, mais elle dit où lire : le Location doit
    // pointer sur une ressource que le GET sert vraiment.
    [Test]
    public async Task Get_ShouldServeTheAccount_WhenFollowingTheLocationOfTheCreation()
    {
        // Arrange

        var created = await OpenAsync(DepositIban);

        // Act

        var response = await _client.GetAsync(created.Headers.Location);

        // Assert

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await BodyOf(response)).GetProperty("iban").GetString().Should().Be(DepositIban);
    }

    [Test]
    public async Task Post_ShouldReturn204WithNoBody_WhenMoneyIsDeposited()
    {
        // Arrange

        var created = await OpenAsync("GR9608100010000001234567890");

        // Act

        var response = await DepositAsync(
            location: created.Headers.Location!,
            amount: 25m
        );

        // Assert

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    // Le corollaire du 204 : l'état d'après ne s'obtient que par une lecture.
    [Test]
    public async Task Get_ShouldReflectTheCommand_WhenReadAfterADeposit()
    {
        // Arrange

        var created = await OpenAsync("NL91ABNA0417164300");

        await DepositAsync(
            location: created.Headers.Location!,
            amount: 25m
        );

        // Act

        var response = await _client.GetAsync(created.Headers.Location);

        // Assert

        (await BodyOf(response)).GetProperty("balance").GetDecimal().Should().Be(25m);
    }

    private Task<HttpResponseMessage> OpenAsync(string iban) => _client.PostAsJsonAsync(
        requestUri: "api/bank/accounts",
        value: new {
            iban,
            currency = "EUR",
        }
    );

    private Task<HttpResponseMessage> DepositAsync(
        Uri location,
        decimal amount
    ) => _client.PostAsJsonAsync(
        requestUri: $"{location.AbsolutePath}/deposits",
        value: new {
            amount,
            currency = "EUR",
        }
    );

    private static async Task<JsonElement> BodyOf(HttpResponseMessage response) => JsonDocument
        .Parse(await response.Content.ReadAsStringAsync())
        .RootElement;
}
