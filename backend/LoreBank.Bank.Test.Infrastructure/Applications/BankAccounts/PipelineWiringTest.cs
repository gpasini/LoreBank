using System.Transactions;
using LoreBank.Bank.Application.Commands;
using LoreBank.Bank.Application.Queries;
using LoreBank.Bank.Application.Results;
using LoreBank.Bank.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Application.Behaviors;
using MediatR;

namespace LoreBank.Bank.Test.Infrastructure.Applications.BankAccounts;

// Pas de contrainte générique `where TRequest : IMutatingRequest` : le behavior
// est enregistré comme open behavior pour toute requête, et se neutralise
// lui-même au runtime pour ce qui n'est pas un `IMutatingRequest`.
// Le test sur la query vérifie qu'elle ne laisse aucune transaction ambiante.
[TestFixture]
[TestOf(typeof(TransactionBehavior<,>))]
public sealed class PipelineWiringTest : BankIntegrationTest
{
    [Test]
    public void Handle_ShouldWrapTheRequest_WhenRequestIsACommand()
    {
        var behaviors = GetService<IEnumerable<IPipelineBehavior<DepositMoneyCommand, Unit>>>();

        behaviors.Should().ContainItemsAssignableTo<TransactionBehavior<DepositMoneyCommand, Unit>>();
    }

    [Test]
    public async Task Handle_ShouldNotOpenAnAmbientTransaction_WhenRequestIsAQuery()
    {
        // Arrange

        var behaviors =
            GetService<IEnumerable<IPipelineBehavior<GetBankAccountByIdQuery, BankAccountResult>>>();
        Transaction? ambient = null;
        var fakeResult = new BankAccountResult(
            Id: Guid.NewGuid(),
            Iban: "FR7630006000011234567890189",
            Balance: 0m,
            Currency: "EUR",
            IsClosed: false
        );
        BankAccountResult? result = null;

        // Act

        // BaseIntegrationTest tient déjà sa propre transaction ambiante (même
        // niveau d'isolation que le behavior) : un Required se contenterait de
        // la rejoindre silencieusement. On la suspend le temps de l'appel pour
        // qu'une transaction ouverte par erreur redevienne observable.
        using (new TransactionScope(
            scopeOption: TransactionScopeOption.Suppress,
            asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled
        )) {
            foreach (var behavior in behaviors) {
                result = await behavior.Handle(
                    request: new GetBankAccountByIdQuery(Guid.NewGuid()),
                    next: _ => {
                        ambient = Transaction.Current;

                        return Task.FromResult(fakeResult);
                    },
                    cancellationToken: CancellationToken.None
                );
            }
        }

        // Assert

        ambient.Should().BeNull();
        result.Should().BeSameAs(fakeResult);
    }
}
