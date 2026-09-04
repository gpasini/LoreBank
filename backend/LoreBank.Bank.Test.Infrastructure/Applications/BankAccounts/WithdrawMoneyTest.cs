using LoreBank.Bank.Application.Commands.WithdrawMoney;
using LoreBank.Bank.Application.Queries.GetBankAccountById;
using LoreBank.Bank.Domain.Exceptions;
using LoreBank.Bank.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.Bank.Test.Infrastructure.Applications.BankAccounts;

public sealed class WithdrawMoneyTest : BaseIntegrationTest<BankWebAppFactory, DbSetup>
{
    [Test]
    public async Task WithdrawMoney_ShouldDecreaseBalance()
    {
        // Arrange

        await DbSetup.CreateBankAccountAsync(balance: 100m);

        var accountId = DbSetup.GetLastBankAccountId();

        // Act

        await Sender.Send(
            new WithdrawMoneyCommand(
                AccountId: accountId.Value,
                Amount: 30m,
                Currency: "EUR"
            )
        );

        // Assert

        var account = await Sender.Send(new GetBankAccountByIdQuery(accountId.Value));

        account.Balance.Should().Be(70m);
    }

    [Test]
    public async Task WithdrawMoney_ShouldThrow_WhenBalanceIsInsufficient()
    {
        // Arrange

        await DbSetup.CreateBankAccountAsync(balance: 70m);

        // Act & Assert

        var act = () => Sender.Send(
            new WithdrawMoneyCommand(
                AccountId: DbSetup.GetLastBankAccountId().Value,
                Amount: 1000m,
                Currency: "EUR"
            )
        );

        await act.Should().ThrowAsync<InsufficientBalanceException>();
    }

    [Test]
    public async Task WithdrawMoney_ShouldThrow_WhenAmountIsNegative()
    {
        // Arrange

        // Un retrait négatif créditerait le compte (le contrôle de solde ne
        // voit jamais un montant négatif comme supérieur au solde) : refusé
        // par PositiveMoney avant l'agrégat.
        await DbSetup.CreateBankAccountAsync(balance: 70m);

        // Act & Assert

        var act = () => Sender.Send(
            new WithdrawMoneyCommand(
                AccountId: DbSetup.GetLastBankAccountId().Value,
                Amount: -100m,
                Currency: "EUR"
            )
        );

        await act.Should().ThrowAsync<NonPositiveAmountException>();
    }
}
