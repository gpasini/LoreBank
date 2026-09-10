using LoreBank.Bank.Application.Commands.CloseBankAccount;
using LoreBank.Bank.Application.Queries.GetBankAccountById;
using LoreBank.Bank.Domain.Exceptions;
using LoreBank.Bank.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.Bank.Test.Infrastructure.Applications.BankAccounts;

public sealed class CloseBankAccountTest : BaseIntegrationTest<BankWebAppFactory, DbSetup>
{
    [Test]
    public async Task CloseBankAccount_ShouldCloseAccount()
    {
        // Arrange

        await DbSetup.CreateBankAccount().RunAsync();

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

        await DbSetup
            .CreateBankAccount()
            .Deposit(deposit => deposit.WithAmount(70m))
            .RunAsync();

        // Act & Assert

        var act = () => Sender.Send(new CloseBankAccountCommand(DbSetup.GetLastBankAccountId().Value));

        await act.Should().ThrowAsync<NonEmptyAccountClosureException>();
    }
}
