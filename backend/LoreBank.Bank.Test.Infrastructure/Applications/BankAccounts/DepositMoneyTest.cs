using LoreBank.Bank.Application.Commands;
using LoreBank.Bank.Application.Exceptions;
using LoreBank.Bank.Application.Queries;
using LoreBank.Bank.Test.Infrastructure.Setups;

namespace LoreBank.Bank.Test.Infrastructure.Applications.BankAccounts;

public sealed class DepositMoneyTest : BaseIntegrationTest
{
    [Test]
    public async Task DepositMoney_ShouldIncreaseBalance()
    {
        // Arrange

        DbSetup.CreateBankAccount();

        var accountId = DbSetup.GetLastBankAccountId();

        // Act

        await Sender.Send(
            new DepositMoneyCommand(
                AccountId: accountId.Value,
                Amount: 100m,
                Currency: "EUR"
            )
        );

        // Assert

        var account = await Sender.Send(new GetBankAccountByIdQuery(accountId.Value));

        account.Balance.Should().Be(100m);
    }

    [Test]
    public async Task DepositMoney_ShouldThrow_WhenAccountIsUnknown()
    {
        var act = () => Sender.Send(
            new DepositMoneyCommand(
                AccountId: Guid.NewGuid(),
                Amount: 100m,
                Currency: "EUR"
            )
        );

        await act.Should().ThrowAsync<BankAccountNotFoundException>();
    }
}
