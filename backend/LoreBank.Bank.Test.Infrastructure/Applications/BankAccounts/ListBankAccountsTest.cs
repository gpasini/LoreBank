using LoreBank.Bank.Application.Commands.CloseBankAccount;
using LoreBank.Bank.Application.Queries.ListBankAccounts;
using LoreBank.Bank.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.Bank.Test.Infrastructure.Applications.BankAccounts;

// La base est partagée avec les fixtures qui écrivent pour de vrai
// (CqsContractTest) : la liste peut contenir d'autres comptes que ceux
// arrangés ici, les assertions ciblent les nôtres et l'ordre relatif.
public sealed class ListBankAccountsTest : BaseIntegrationTest<BankWebAppFactory, DbSetup>
{
    [Test]
    public async Task ListBankAccounts_ShouldMapEveryColumn_WhenAccountsExist()
    {
        // Arrange

        await DbSetup.CreateBankAccountAsync(
            iban: "FR7630006000011234567890189",
            currency: "EUR",
            balance: 42.50m
        );

        var accountId = DbSetup.GetLastBankAccountId();

        // Act

        var result = await Sender.Send(new ListBankAccountsQuery());

        // Assert

        var account = result.Accounts.Should().ContainSingle(item => item.Id == accountId.Value).Subject;

        account.Iban.Should().Be("FR7630006000011234567890189");
        account.Balance.Should().Be(42.50m);
        account.Currency.Should().Be("EUR");
        account.IsClosed.Should().BeFalse();
    }

    [Test]
    public async Task ListBankAccounts_ShouldOrderByIban_AndReportClosures()
    {
        // Arrange

        await DbSetup.CreateBankAccountAsync(iban: "NL91ABNA0417164300");

        var later = DbSetup.GetLastBankAccountId();

        await DbSetup.CreateBankAccountAsync(iban: "BE68539007547034");

        var earlier = DbSetup.GetLastBankAccountId();

        await Sender.Send(new CloseBankAccountCommand(earlier.Value));

        // Act

        var result = await Sender.Send(new ListBankAccountsQuery());

        // Assert

        var ibans = result.Accounts.Select(item => item.Iban).ToList();

        ibans.IndexOf("BE68539007547034").Should().BeLessThan(ibans.IndexOf("NL91ABNA0417164300"));
        result.Accounts.Single(item => item.Id == earlier.Value).IsClosed.Should().BeTrue();
        result.Accounts.Single(item => item.Id == later.Value).IsClosed.Should().BeFalse();
    }
}
