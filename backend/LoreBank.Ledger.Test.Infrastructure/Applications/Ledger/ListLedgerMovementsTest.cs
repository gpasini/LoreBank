using LoreBank.Bank.Contracts.IntegrationEvents;
using LoreBank.Ledger.Application.Exceptions;
using LoreBank.Ledger.Application.IntegrationEvents;
using LoreBank.Ledger.Application.Queries.ListLedgerMovements;
using LoreBank.Ledger.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Application;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.Ledger.Test.Infrastructure.Applications.Ledger;

// Les écritures du Ledger naissent des integration events : l'arrange joue
// les handlers eux-mêmes — le vrai use case d'écriture du module — sous le
// TransactionScope rollbacké de la fixture. La mécanique de la Liste
// (bornes, jokers, facettes disjonctives) est prouvée par le socle
// (ListContractTest) ; ici, que le Ledger l'emprunte, sous sa ressource.
public sealed class ListLedgerMovementsTest : BaseIntegrationTest<LedgerWebAppFactory, DbSetup>
{
    private static readonly DateTimeOffset RecordingInstant = new(
        year: 2026,
        month: 9,
        day: 9,
        hour: 8,
        minute: 30,
        second: 0,
        offset: TimeSpan.Zero
    );

    // Le port publié de Bank rend null pour un compte inconnu ; c'est le
    // handler de query qui en fait l'erreur métier du Ledger, avec son code —
    // l'absence de la ressource, pas une Page vide.
    [Test]
    public async Task ListLedgerMovements_ShouldThrow_WhenBankAccountIsUnknown()
    {
        var act = () => Sender.Send(new ListLedgerMovementsQuery { AccountId = Guid.NewGuid() });

        await act.Should().ThrowAsync<UnknownBankAccountException>();
    }

    // Le reader joint deux rows keyless (ADR 0018) dont le mapping colonne →
    // propriété est en chaînes que rien ne compile : ce test relit chaque
    // champ de l'item — venu des lignes et de leur écriture.
    [Test]
    public async Task ListLedgerMovements_ShouldMapEveryColumn()
    {
        // Arrange

        await DbSetup.CreateBankAccountAsync(iban: "FR7630006000011234567890189");

        var accountId = DbSetup.GetLastBankAccountId();

        Factory.TimeProvider.Instant = RecordingInstant;

        await DepositAsync(
            accountId: accountId,
            amount: 25.50m
        );

        // Act

        var page = await Sender.Send(new ListLedgerMovementsQuery { AccountId = accountId });

        // Assert

        page.TotalCount.Should().Be(1);

        var movement = page.Items.Should().ContainSingle().Subject;

        movement.EntryId.Should().NotBeEmpty();
        movement.Direction.Should().Be("Credit");
        movement.Amount.Should().Be(25.50m);
        movement.Currency.Should().Be("EUR");
        movement.RecordedAt.Should().Be(RecordingInstant);
    }

    // L'ordre est celui de la comptabilisation (ADR 0024), du plus récent au
    // plus ancien : le retrait, comptabilisé en second mais à un Instant
    // antérieur, sort en dernier — l'identifiant d'écriture ne décide de rien.
    [Test]
    public async Task ListLedgerMovements_ShouldOrderByRecordingInstant_NewestFirst()
    {
        // Arrange

        await DbSetup.CreateBankAccountAsync(iban: "IT60X0542811101000000123456");

        var accountId = DbSetup.GetLastBankAccountId();

        Factory.TimeProvider.Instant = RecordingInstant;

        await DepositAsync(
            accountId: accountId,
            amount: 25.50m
        );

        Factory.TimeProvider.Instant = RecordingInstant.AddHours(-1);

        await WithdrawAsync(
            accountId: accountId,
            amount: 10m
        );

        // Act

        var page = await Sender.Send(new ListLedgerMovementsQuery { AccountId = accountId });

        // Assert

        page.Items.Select(movement => movement.Direction).Should().Equal(
            "Credit",
            "Debit"
        );
        page.Items.Select(movement => movement.RecordedAt).Should().Equal(
            RecordingInstant,
            RecordingInstant.AddHours(-1)
        );
    }

    // La jambe trésorerie de chaque écriture ne regarde pas ce compte : un
    // mouvement par écriture, du sens du compte client — et la facette
    // `direction` les compte, sous le nom du filtre de la query.
    [Test]
    public async Task ListLedgerMovements_ShouldListOneMovementPerTouchingLine_AndFacetTheDirection()
    {
        // Arrange

        await DbSetup.CreateBankAccountAsync(iban: "DE89370400440532013000");

        var accountId = DbSetup.GetLastBankAccountId();

        await DepositAsync(
            accountId: accountId,
            amount: 25.50m
        );
        await DepositAsync(
            accountId: accountId,
            amount: 4.50m
        );
        await WithdrawAsync(
            accountId: accountId,
            amount: 10m
        );

        // Act

        var page = await Sender.Send(new ListLedgerMovementsQuery { AccountId = accountId });
        var debits = await Sender.Send(new ListLedgerMovementsQuery {
                AccountId = accountId,
                Direction = ["Debit"],
            }
        );

        // Assert

        page.TotalCount.Should().Be(3);
        page.Facets.Should().ContainSingle().Which.Should().BeEquivalentTo(
            expectation: new Facet(
                Name: "direction",
                Values: [
                    new FacetValue(
                        Value: "Credit",
                        Count: 2
                    ),
                    new FacetValue(
                        Value: "Debit",
                        Count: 1
                    ),
                ]
            ),
            config: options => options.WithStrictOrdering()
        );

        debits.Items.Should().ContainSingle().Which.Amount.Should().Be(10m);
        debits.Facets.Single().Values.Should().HaveCount(2, "une facette est comptée hors de son propre filtre");
    }

    // La recherche porte sur l'identifiant d'écriture : ses premiers
    // caractères, ceux que l'écran affiche, retrouvent l'écriture.
    [Test]
    public async Task ListLedgerMovements_ShouldSearchTheEntryId()
    {
        // Arrange

        await DbSetup.CreateBankAccountAsync(iban: "NL91ABNA0417164300");

        var accountId = DbSetup.GetLastBankAccountId();

        await DepositAsync(
            accountId: accountId,
            amount: 1m
        );
        await DepositAsync(
            accountId: accountId,
            amount: 2m
        );

        var wanted = (await Sender.Send(new ListLedgerMovementsQuery { AccountId = accountId })).Items
            .Single(movement => movement.Amount == 2m);

        // Act

        var page = await Sender.Send(new ListLedgerMovementsQuery {
                AccountId = accountId,
                Search = wanted.EntryId.ToString()[..8].ToUpperInvariant(),
            }
        );

        // Assert

        page.Items.Should().ContainSingle().Which.EntryId.Should().Be(wanted.EntryId);
    }

    // Un compte sans mouvement a un ledger vide : pour la Liste, l'absence
    // est un résultat normal — seule l'absence du compte est une erreur.
    [Test]
    public async Task ListLedgerMovements_ShouldReturnAnEmptyPage_WhenTheAccountHasNoMovement()
    {
        // Arrange

        await DbSetup.CreateBankAccountAsync(iban: "BE68539007547034");

        // Act

        var page = await Sender.Send(new ListLedgerMovementsQuery { AccountId = DbSetup.GetLastBankAccountId() });

        // Assert

        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
    }

    private Task DepositAsync(
        Guid accountId,
        decimal amount
    ) => GetService<MoneyDepositedIntegrationEventHandler>().HandleAsync(
        integrationEvent: new MoneyDepositedIntegrationEvent(
            AccountId: accountId,
            Amount: amount,
            Currency: "EUR"
        ),
        cancellationToken: CancellationToken.None
    );

    private Task WithdrawAsync(
        Guid accountId,
        decimal amount
    ) => GetService<MoneyWithdrawnIntegrationEventHandler>().HandleAsync(
        integrationEvent: new MoneyWithdrawnIntegrationEvent(
            AccountId: accountId,
            Amount: amount,
            Currency: "EUR"
        ),
        cancellationToken: CancellationToken.None
    );
}
