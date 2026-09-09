using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoreBank.Ledger.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.Ledger.Test.Infrastructure.Apis;

// Le bout en bout du chantier communication inter-modules, en vrai et par le
// bord HTTP (BaseHostTest : le TransactionScope ambiant ne traverse pas la
// frontière HTTP — tout est commité, d'où un IBAN propre) : un dépôt chez
// Bank → outbox → une passe du dispatcher → écriture équilibrée chez Ledger →
// GET enrichi par le port de lecture publié. Épingle aussi l'ensemble exact
// des clés JSON du GET (ADR 0012) : renommer une propriété du Result est un
// breaking change HTTP, il doit rougir ici.
[TestFixture]
public sealed class CqsContractTest : BaseHostTest<LedgerWebAppFactory>
{
    private const string FlowIban = "ES9121000418450200051332";

    private static readonly DateTimeOffset DepositInstant = new(
        year: 2026,
        month: 9,
        day: 9,
        hour: 8,
        minute: 30,
        second: 0,
        offset: TimeSpan.Zero
    );

    private static readonly DateTimeOffset RecordingInstant = DepositInstant.AddMinutes(5);

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
    public async Task Get_ShouldServeTheLedgerFedByBankEvents_WhenFollowingTheFullFlow()
    {
        // Arrange — le fait métier naît chez Bank, par son API, à un premier
        // Instant de l'horloge du harnais.

        Factory.TimeProvider.Instant = DepositInstant;

        var opened = await _client.PostAsJsonAsync(
            requestUri: "api/bank/accounts",
            value: new {
                iban = FlowIban,
                currency = "EUR",
            }
        );

        var location = opened.Headers.Location!;
        var accountId = Guid.Parse(location.AbsolutePath.Split('/').Last());

        (await _client.PostAsJsonAsync(
            requestUri: $"{location.AbsolutePath}/deposits",
            value: new {
                amount = 25.50m,
                currency = "EUR",
            }
        )).EnsureSuccessStatusCode();

        // Act — la livraison asynchrone, pilotée (la cadence de fond est
        // neutralisée par le harnais), à un second Instant : celui que le
        // Ledger comptabilise (ADR 0024).

        Factory.TimeProvider.Instant = RecordingInstant;

        await Factory.Services
            .GetRequiredService<OutboxProcessor>()
            .ProcessPendingAsync(CancellationToken.None);

        var response = await _client.GetAsync($"api/ledger/bank-accounts/{accountId}");

        // Assert

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        // La forme servie est la surface Application (ADR 0012) : l'ensemble
        // exact des clés, racine et mouvement.
        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "accountId",
            "iban",
            "movements"
        );

        body.GetProperty("accountId").GetGuid().Should().Be(accountId);
        body.GetProperty("iban").GetString().Should().Be(FlowIban);

        var movements = body.GetProperty("movements").EnumerateArray().ToList();
        var movement = movements.Should().ContainSingle().Subject;

        movement.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "entryId",
            "direction",
            "amount",
            "currency",
            "recordedAt"
        );

        movement.GetProperty("direction").GetString().Should().Be("Credit");
        movement.GetProperty("amount").GetDecimal().Should().Be(25.50m);
        movement.GetProperty("currency").GetString().Should().Be("EUR");
        movement.GetProperty("recordedAt").GetDateTimeOffset().Should().Be(RecordingInstant);
    }

    // Le 404 d'une lecture porte un code — celui du Ledger, pas celui de Bank :
    // l'absence traverse le port publié puis redevient une erreur du module.
    [Test]
    public async Task Get_ShouldReturn404WithTheLedgerCode_WhenBankAccountIsUnknown()
    {
        var response = await _client.GetAsync($"api/ledger/bank-accounts/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        body.GetProperty("code").GetString().Should().Be("LEDGER.UNKNOWN_BANK_ACCOUNT");
    }
}
