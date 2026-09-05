using LoreBank.Bank.Contracts.IntegrationEvents;
using LoreBank.Ledger.Application.Exceptions;
using LoreBank.Ledger.Application.IntegrationEvents;
using LoreBank.Ledger.Application.Queries.GetBankAccountLedger;
using LoreBank.Ledger.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.Ledger.Test.Infrastructure.Applications.Ledger;

// Les écritures du Ledger naissent des integration events : l'arrange joue
// les handlers eux-mêmes — le vrai use case d'écriture du module — sous le
// TransactionScope rollbacké de la fixture.
public sealed class GetBankAccountLedgerTest : BaseIntegrationTest<LedgerWebAppFactory, DbSetup>
{
    // Le port publié de Bank rend null pour un compte inconnu ; c'est le
    // handler de query qui en fait l'erreur métier du Ledger, avec son code.
    [Test]
    public async Task GetBankAccountLedger_ShouldThrow_WhenBankAccountIsUnknown()
    {
        var act = () => Sender.Send(new GetBankAccountLedgerQuery(Guid.NewGuid()));

        await act.Should().ThrowAsync<UnknownBankAccountException>();
    }

    // Le SQL du reader est écrit à la main : ce test relit chaque champ du
    // Result — l'iban venu du port de Bank, les mouvements venus des lignes.
    [Test]
    public async Task GetBankAccountLedger_ShouldMapEveryColumn_AndEnrichWithTheIban()
    {
        // Arrange

        await DbSetup.CreateBankAccountAsync(iban: "FR7630006000011234567890189");

        var accountId = DbSetup.GetLastBankAccountId();

        await GetService<MoneyDepositedIntegrationEventHandler>().HandleAsync(
            integrationEvent: new MoneyDepositedIntegrationEvent(
                AccountId: accountId,
                Amount: 25.50m,
                Currency: "EUR"
            ),
            cancellationToken: CancellationToken.None
        );

        // Act

        var ledger = await Sender.Send(new GetBankAccountLedgerQuery(accountId));

        // Assert

        ledger.AccountId.Should().Be(accountId);
        ledger.Iban.Should().Be("FR7630006000011234567890189");

        var movement = ledger.Movements.Should().ContainSingle().Subject;

        movement.EntryId.Should().NotBeEmpty();
        movement.Direction.Should().Be("Credit");
        movement.Amount.Should().Be(25.50m);
        movement.Currency.Should().Be("EUR");
    }

    [Test]
    public async Task GetBankAccountLedger_ShouldListOneMovementPerTouchingLine()
    {
        // Arrange

        await DbSetup.CreateBankAccountAsync(iban: "DE89370400440532013000");

        var accountId = DbSetup.GetLastBankAccountId();

        await GetService<MoneyDepositedIntegrationEventHandler>().HandleAsync(
            integrationEvent: new MoneyDepositedIntegrationEvent(
                AccountId: accountId,
                Amount: 25.50m,
                Currency: "EUR"
            ),
            cancellationToken: CancellationToken.None
        );
        await GetService<MoneyWithdrawnIntegrationEventHandler>().HandleAsync(
            integrationEvent: new MoneyWithdrawnIntegrationEvent(
                AccountId: accountId,
                Amount: 10m,
                Currency: "EUR"
            ),
            cancellationToken: CancellationToken.None
        );

        // Act

        var ledger = await Sender.Send(new GetBankAccountLedgerQuery(accountId));

        // Assert

        // La jambe trésorerie de chaque écriture ne regarde pas ce compte :
        // un mouvement par écriture, du sens du compte client.
        ledger.Movements.Should().HaveCount(2);
        ledger.Movements.Select(movement => movement.Direction)
            .Should().BeEquivalentTo("Credit", "Debit");
    }

    // Un compte sans mouvement a un ledger vide : pour la liste, l'absence
    // est un résultat normal — seule l'absence du compte est une erreur.
    [Test]
    public async Task GetBankAccountLedger_ShouldReturnAnEmptyList_WhenTheAccountHasNoMovement()
    {
        // Arrange

        await DbSetup.CreateBankAccountAsync(iban: "NL91ABNA0417164300");

        // Act

        var ledger = await Sender.Send(new GetBankAccountLedgerQuery(DbSetup.GetLastBankAccountId()));

        // Assert

        ledger.Movements.Should().BeEmpty();
    }
}
