using System.Net.Http.Json;
using System.Text.Json;
using LoreBank.Bank.Infrastructure.Persistence;
using LoreBank.Bank.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.Bank.Test.Infrastructure.Apis;

// Le bout en bout de la publication : une commande HTTP (commit réel, d'où
// BaseHostTest et un IBAN propre) → domain event → handler de mapping →
// ligne d'outbox — puis une passe du dispatcher, qui la marque livrée (aucun
// consommateur n'est monté dans cet hôte : livré à personne est livré). Le
// module de référence prouve qu'il emprunte vraiment le chemin de l'ADR 0014.
[TestFixture]
public sealed class IntegrationEventPublicationTest : BaseHostTest<BankWebAppFactory>
{
    private const string PublicationIban = "IT60X0542811101000000123456";

    private HttpClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        _client = Factory.CreateClient();
        await CleanOutboxAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await CleanOutboxAsync();
    }

    [Test]
    public async Task Deposit_ShouldWriteThePublishedTwinToTheOutbox_AndTheDispatcherShouldDeliverIt()
    {
        // Arrange

        var opened = await _client.PostAsJsonAsync(
            requestUri: "api/bank/accounts",
            value: new {
                iban = PublicationIban,
                currency = "EUR",
            }
        );

        var location = opened.Headers.Location!;
        var accountId = Guid.Parse(location.AbsolutePath.Split('/').Last());

        // Act — le dépôt et le retrait publient, l'ouverture et la clôture non.

        (await _client.PostAsJsonAsync(
            requestUri: $"{location.AbsolutePath}/deposits",
            value: new {
                amount = 25.50m,
                currency = "EUR",
            }
        )).EnsureSuccessStatusCode();

        (await _client.PostAsJsonAsync(
            requestUri: $"{location.AbsolutePath}/withdrawals",
            value: new {
                amount = 10m,
                currency = "EUR",
            }
        )).EnsureSuccessStatusCode();

        // Assert — les deux jumeaux publiés sont dans l'outbox, payload camelCase.

        var rows = await ReadOutboxAsync();

        rows.Should().HaveCount(2);

        var deposited = rows.Single(row => row.Discriminant == "bank.money-deposited");

        deposited.Dispatched.Should().BeFalse();

        var payload = JsonDocument.Parse(deposited.Payload).RootElement;

        payload.GetProperty("accountId").GetGuid().Should().Be(accountId);
        payload.GetProperty("amount").GetDecimal().Should().Be(25.50m);
        payload.GetProperty("currency").GetString().Should().Be("EUR");

        rows.Should().ContainSingle(row => row.Discriminant == "bank.money-withdrawn");

        // Act — une passe du dispatcher.

        await Factory.Services
            .GetRequiredService<OutboxProcessor>()
            .ProcessPendingAsync(CancellationToken.None);

        // Assert

        (await ReadOutboxAsync()).Should().OnlyContain(row => row.Dispatched);
    }

    private sealed record OutboxRow(
        string Discriminant,
        string Payload,
        bool Dispatched
    );

    private static async Task<IReadOnlyList<OutboxRow>> ReadOutboxAsync()
    {
        using var scope = Factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<BankDbContext>();

        await dbContext.Database.OpenConnectionAsync();

        try {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();

            command.CommandText =
                $"SELECT discriminant, payload, dispatched_at IS NOT NULL FROM {dbContext.Schema}.__outbox";

            var rows = new List<OutboxRow>();

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync()) {
                rows.Add(new OutboxRow(
                    Discriminant: reader.GetString(0),
                    Payload: reader.GetString(1),
                    Dispatched: reader.GetBoolean(2)
                ));
            }

            return rows;
        }
        finally {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    // Les autres fixtures HTTP (CqsContractTest) déposent pour de vrai elles
    // aussi : l'outbox du conteneur partagé se nettoie avant ET après, pour
    // que ce test ne compte que ses propres lignes.
    private static async Task CleanOutboxAsync()
    {
        using var scope = Factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<BankDbContext>();

        await dbContext.Database.OpenConnectionAsync();

        try {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();

            command.CommandText = $"DELETE FROM {dbContext.Schema}.__outbox";

            await command.ExecuteNonQueryAsync();
        }
        finally {
            await dbContext.Database.CloseConnectionAsync();
        }
    }
}
