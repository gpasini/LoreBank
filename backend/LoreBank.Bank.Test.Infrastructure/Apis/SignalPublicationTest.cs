using System.Net.Http.Json;
using LoreBank.Bank.Infrastructure.Persistence;
using LoreBank.Bank.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.Bank.Test.Infrastructure.Apis;

// Le bout en bout du Signal (ADR 0026) : un client abonné au compte, un
// dépôt en HTTP, une passe de livraison (le Ledger consomme), une passe du
// suiveur — et le client reçoit « bank.money-deposited sur ce compte ». Le
// module de référence prouve qu'il emprunte vraiment le chemin ; la
// plomberie du flux est prouvée côté socle (SignalContractTest). Même
// partage qu'entre IntegrationEventPublicationTest et OutboxProcessorTest.
[TestFixture]
public sealed class SignalPublicationTest : BaseHostTest<BankWebAppFactory>
{
    private const string SignalIban = "ES9121000418450200051332";

    private readonly static TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private HttpClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        _client = Factory.CreateClient();
        await OutboxProbe.CleanAsync<BankDbContext>(Factory);

        // Le suiveur s'amorce à son premier passage.
        await SignalProbe.TailAsync(Factory);
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await OutboxProbe.CleanAsync<BankDbContext>(Factory);
    }

    [Test]
    public async Task Deposit_ShouldSignalTheAccount_OnceThePublishedTwinIsDelivered()
    {
        // Arrange — un compte, et un client abonné à lui seul.

        var opened = await _client.PostAsJsonAsync(
            requestUri: "api/bank/accounts",
            value: new {
                iban = SignalIban,
                currency = "EUR",
            }
        );

        var location = opened.Headers.Location!;
        var accountId = Guid.Parse(location.AbsolutePath.Split('/').Last());

        await using var stream = await SignalProbe.OpenAsync(
            client: _client,
            query: $"?resource=bank-account/{accountId}"
        );

        // Act — le dépôt, sa livraison, le suivi.

        (await _client.PostAsJsonAsync(
            requestUri: $"{location.AbsolutePath}/deposits",
            value: new {
                amount = 25.50m,
                currency = "EUR",
            }
        )).EnsureSuccessStatusCode();

        await OutboxProbe.DeliverAsync(Factory);
        await SignalProbe.TailAsync(Factory);

        // Assert — le Signal dit quoi et où, jamais comment.

        var signal = await stream.ReadSignalAsync(Timeout);

        signal.GetProperty("discriminant").GetString().Should().Be("bank.money-deposited");
        signal.GetProperty("resourceKind").GetString().Should().Be("bank-account");
        signal.GetProperty("resourceId").GetGuid().Should().Be(accountId);
        signal.TryGetProperty(
            propertyName: "amount",
            value: out _
        ).Should().BeFalse("un Signal ne porte pas l'état : le client refait son GET");
    }
}
