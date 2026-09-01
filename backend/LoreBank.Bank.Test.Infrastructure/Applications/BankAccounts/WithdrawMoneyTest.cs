using LoreBank.Bank.Application.Commands;
using LoreBank.Bank.Application.Queries;
using LoreBank.Bank.Domain.Exceptions;
using LoreBank.Bank.Test.Infrastructure.Setups;

namespace LoreBank.Bank.Test.Infrastructure.Applications.BankAccounts;

public sealed class WithdrawMoneyTest : BaseIntegrationTest
{
    [Test]
    public async Task WithdrawMoney_ShouldDecreaseBalance()
    {
        // Arrange

        DbSetup.CreateBankAccount(balance: 100m);

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

        DbSetup.CreateBankAccount(balance: 70m);

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
}
