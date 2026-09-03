using LoreBank.Bank.Application.Commands;
using LoreBank.Bank.Application.Queries;
using LoreBank.Bank.Domain.Exceptions;
using LoreBank.Bank.Test.Infrastructure.Setups;

namespace LoreBank.Bank.Test.Infrastructure.Applications.BankAccounts;

public sealed class CloseBankAccountTest : BankIntegrationTest
{
    [Test]
    public async Task CloseBankAccount_ShouldCloseAccount()
    {
        // Arrange

        await DbSetup.CreateBankAccountAsync();

        var accountId = DbSetup.GetLastBankAccountId();

        // Act

        await Sender.Send(new CloseBankAccountCommand(accountId.Value));

        // Assert

        var account = await Sender.Send(new GetBankAccountByIdQuery(accountId.Value));

        account.IsClosed.Should().BeTrue();
    }

    [Test]
    public async Task CloseBankAccount_ShouldThrow_WhenBalanceIsNotZero()
    {
        // Arrange

        await DbSetup.CreateBankAccountAsync(balance: 70m);

        // Act & Assert

        var act = () => Sender.Send(new CloseBankAccountCommand(DbSetup.GetLastBankAccountId().Value));

        await act.Should().ThrowAsync<NonEmptyAccountClosureException>();
    }
}
