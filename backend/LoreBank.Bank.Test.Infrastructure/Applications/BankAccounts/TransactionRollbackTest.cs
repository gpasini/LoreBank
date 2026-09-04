using LoreBank.Bank.Application.Commands.OpenBankAccount;
using LoreBank.Bank.Domain.Aggregates;
using LoreBank.Bank.Infrastructure.Persistence;
using LoreBank.Bank.Test.Infrastructure.Fakes;
using LoreBank.Bank.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Domain.ValueObjects;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.Bank.Test.Infrastructure.Applications.BankAccounts;

// Hérite de BaseHostTest, pas de BaseIntegrationTest : un TransactionScope interne
// non complété condamne le scope ambiant, et toute lecture ultérieure lèverait
// TransactionAbortedException. Ce test n'a rien à nettoyer — le rollback est le
// nettoyage.
[TestFixture]
[TestOf(typeof(OpenBankAccountCommand))]
public sealed class TransactionRollbackTest : BaseHostTest<BankWebAppFactory>
{
    // Ne pas nommer ces constantes `Iban` : cela masquerait le type `Iban` du
    // SharedKernel dans toute la classe.
    //
    // Deux IBAN distincts, volontairement : le test témoin committe pour de
    // vrai, donc partager un IBAN avec le test de rollback ferait dépendre ce
    // dernier de l'ordre d'exécution des tests (le témoin laisserait une ligne
    // que la requête du rollback trouverait).
    private const string RollbackAttemptIban = "DE89370400440532013000";
    private const string WitnessAccountIban = "GB33BUKB20201555555555";

    [Test]
    public async Task OpenBankAccount_ShouldPersistNothing_WhenADomainEventHandlerThrows()
    {
        // Arrange

        using var scope = Factory.Services.CreateScope();
        var sender = Factory.Services.GetRequiredService<ConfigurableWelcomeLetterSender>();
        sender.ThrowOnSend = new InvalidOperationException("boom");

        // Act

        var act = async () => await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new OpenBankAccountCommand(
                Iban: RollbackAttemptIban,
                Currency: "EUR"
            )
        );

        // Assert

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");

        using var readScope = Factory.Services.CreateScope();
        var context = readScope.ServiceProvider.GetRequiredService<BankDbContext>();

        (await context.BankAccounts.AnyAsync(account => account.Iban == new Iban(RollbackAttemptIban)))
            .Should().BeFalse();
    }

    [Test]
    public async Task OpenBankAccount_ShouldPersistTheAccount_WhenNoHandlerThrows()
    {
        // Arrange

        using var scope = Factory.Services.CreateScope();

        // Act

        var accountId = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new OpenBankAccountCommand(
                Iban: WitnessAccountIban,
                Currency: "EUR"
            )
        );

        // Assert

        using var readScope = Factory.Services.CreateScope();
        var context = readScope.ServiceProvider.GetRequiredService<BankDbContext>();

        (await context.BankAccounts.AnyAsync(account => account.Id == new BankAccountId(accountId)))
            .Should().BeTrue();
    }
}
