using LoreBank.Bank.Application.Commands.OpenBankAccount;
using LoreBank.Bank.Domain.Aggregates;
using LoreBank.Bank.Domain.Repositories;
using LoreBank.Bank.Infrastructure.Persistence;
using LoreBank.Bank.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.ValueObjects;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.Bank.Test.Infrastructure.Persistence;

// La Version d'agrégat (ADR 0020) est une convention du socle : le module de
// référence n'a rien déclaré et doit pourtant en bénéficier. Deux scopes
// chargent le même compte, déposent, sauvent : la seconde écriture porte une
// version périmée et doit être refusée — et rien de ce qu'elle voulait faire
// ne doit rester, ni solde ni ligne d'outbox.
//
// BaseHostTest, pas BaseIntegrationTest : les deux sauvegardes doivent
// committer pour de vrai, l'une après l'autre — c'est l'entrelacement qu'on
// contrôle, pas un qu'on subit en tirant deux requêtes HTTP en parallèle.
[TestFixture]
[TestOf(typeof(IBankAccountRepository))]
public sealed class ConcurrentUpdateTest : BaseHostTest<BankWebAppFactory>
{
    private const string ConcurrentIban = "ES9121000418450200051332";

    [SetUp]
    public Task SetUp() => OutboxProbe.CleanAsync<BankDbContext>(Factory);

    [TearDown]
    public Task TearDown() => OutboxProbe.CleanAsync<BankDbContext>(Factory);

    [Test]
    public async Task SaveAsync_ShouldRefuseTheSecondWriter_WhenTwoScopesLoadedTheSameVersion()
    {
        // Arrange — un compte committé, chargé par deux scopes distincts.

        BankAccountId accountId;

        using (var setupScope = Factory.Services.CreateScope()) {
            accountId = BankAccountId.Hydrate(
                await setupScope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new OpenBankAccountCommand(
                        Iban: ConcurrentIban,
                        Currency: "EUR"
                    )
                )
            );
        }

        using var firstScope = Factory.Services.CreateScope();
        using var secondScope = Factory.Services.CreateScope();

        var firstRepository = firstScope.ServiceProvider.GetRequiredService<IBankAccountRepository>();
        var secondRepository = secondScope.ServiceProvider.GetRequiredService<IBankAccountRepository>();

        var firstView = await firstRepository.GetRequiredByIdAsync(
            id: accountId,
            cancellationToken: CancellationToken.None
        );
        var secondView = await secondRepository.GetRequiredByIdAsync(
            id: accountId,
            cancellationToken: CancellationToken.None
        );

        firstView.Deposit(PositiveMoney.Of(
            amount: 10m,
            currency: "EUR"
        ));
        secondView.Deposit(PositiveMoney.Of(
            amount: 20m,
            currency: "EUR"
        ));

        // Act — le premier écrit, le second écrit sur une version périmée.

        await firstRepository.SaveAsync(
            account: firstView,
            cancellationToken: CancellationToken.None
        );

        var act = () => secondRepository.SaveAsync(
            account: secondView,
            cancellationToken: CancellationToken.None
        );

        // Assert — refusé, avec la clé en primitive ; seul le premier dépôt
        // existe, en base comme dans l'outbox.

        (await act.Should().ThrowAsync<ConcurrentUpdateException>())
            .Which.Parameters["id"].Should().Be(accountId.Value);

        using var readScope = Factory.Services.CreateScope();
        var context = readScope.ServiceProvider.GetRequiredService<BankDbContext>();

        var balance = await context.BankAccounts
            .Where(account => account.Id == accountId)
            .Select(account => account.Balance.Amount)
            .SingleAsync();

        balance.Should().Be(10m);

        (await OutboxProbe.ReadRowsAsync<BankDbContext>(Factory))
            .Should().ContainSingle(row => row.Discriminant == "bank.money-deposited");
    }
}
