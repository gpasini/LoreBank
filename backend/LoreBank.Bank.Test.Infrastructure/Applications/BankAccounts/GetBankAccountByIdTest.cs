using LoreBank.Bank.Application.Commands.CloseBankAccount;
using LoreBank.Bank.Application.Exceptions;
using LoreBank.Bank.Application.Queries.GetBankAccountById;
using LoreBank.Bank.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.Bank.Test.Infrastructure.Applications.BankAccounts;

public sealed class GetBankAccountByIdTest : BaseIntegrationTest<BankWebAppFactory, DbSetup>
{
    // L'absence est une erreur métier, pas un `null` : c'est ce qui donne un
    // code au 404 d'une lecture comme à celui d'une commande.
    [Test]
    public async Task GetBankAccountById_ShouldThrow_WhenAccountIsUnknown()
    {
        // Act

        var act = () => Sender.Send(new GetBankAccountByIdQuery(Guid.NewGuid()));

        // Assert

        await act.Should().ThrowAsync<BankAccountNotFoundException>();
    }

    // Le SQL du reader est écrit à la main : rien ne relie ses colonnes aux
    // propriétés du DTO à la compilation. Ce test est le seul garde-fou — il
    // relit chaque champ, y compris ceux qu'une inversion de colonnes rendrait
    // silencieusement faux.
    [Test]
    public async Task GetBankAccountById_ShouldMapEveryColumn_WhenAccountExists()
    {
        // Arrange

        await DbSetup.CreateBankAccountAsync(
            iban: "FR7630006000011234567890189",
            currency: "EUR",
            balance: 42.50m
        );

        var accountId = DbSetup.GetLastBankAccountId();

        // Act

        var account = await Sender.Send(new GetBankAccountByIdQuery(accountId.Value));

        // Assert

        account.Id.Should().Be(accountId.Value);
        account.Iban.Should().Be("FR7630006000011234567890189");
        account.Balance.Should().Be(42.50m);
        account.Currency.Should().Be("EUR");
        account.IsClosed.Should().BeFalse();
        account.OpenedBy.Should().BeNull();
    }

    [Test]
    public async Task GetBankAccountById_ShouldReportTheClosure_WhenAccountIsClosed()
    {
        // Arrange

        await DbSetup.CreateBankAccountAsync();

        var accountId = DbSetup.GetLastBankAccountId();

        await Sender.Send(new CloseBankAccountCommand(accountId.Value));

        // Act

        var account = await Sender.Send(new GetBankAccountByIdQuery(accountId.Value));

        // Assert

        account.IsClosed.Should().BeTrue();
    }
}
