using LoreBank.Bank.Application.Commands.DepositMoney;
using LoreBank.Bank.Application.Exceptions;
using LoreBank.Bank.Application.Queries.GetBankAccountById;
using LoreBank.Bank.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.Bank.Test.Infrastructure.Applications.BankAccounts;

public sealed class DepositMoneyTest : BaseIntegrationTest<BankWebAppFactory, DbSetup>
{
    [Test]
    public async Task DepositMoney_ShouldIncreaseBalance()
    {
        // Arrange

        await DbSetup.CreateBankAccountAsync();

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

    [Test]
    public async Task DepositMoney_ShouldThrow_WhenAmountIsNegative()
    {
        // Arrange

        // Un dépôt négatif débiterait le compte en contournant le contrôle de
        // solde du retrait : refusé par PositiveMoney avant l'agrégat.
        await DbSetup.CreateBankAccountAsync();

        var accountId = DbSetup.GetLastBankAccountId();

        // Act & Assert

        var act = () => Sender.Send(
            new DepositMoneyCommand(
                AccountId: accountId.Value,
                Amount: -100m,
                Currency: "EUR"
            )
        );

        await act.Should().ThrowAsync<NonPositiveAmountException>();
    }
}
