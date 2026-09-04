using LoreBank.Bank.Application.Commands.OpenBankAccount;
using LoreBank.Bank.Application.Queries.GetBankAccountById;
using LoreBank.Bank.Test.Infrastructure.Fakes;
using LoreBank.Bank.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.Bank.Test.Infrastructure.Applications.BankAccounts;

public sealed class OpenBankAccountTest : BaseIntegrationTest<BankWebAppFactory, DbSetup>
{
    [Test]
    public async Task OpenBankAccount_ShouldPersistAccount()
    {
        // Act

        var accountId = await Sender.Send(
            new OpenBankAccountCommand(
                Iban: "fr76 3000 6000 0112 3456 7890 189",
                Currency: "EUR"
            )
        );

        // Assert

        var account = await Sender.Send(new GetBankAccountByIdQuery(accountId));

        account.Should().NotBeNull();
        account.Iban.Should().Be("FR7630006000011234567890189");
        account.Balance.Should().Be(0m);
    }

    [Test]
    public async Task OpenBankAccount_ShouldThrow_WhenIbanIsInvalid()
    {
        var act = () => Sender.Send(
            new OpenBankAccountCommand(
                Iban: "XX",
                Currency: "EUR"
            )
        );

        await act.Should().ThrowAsync<InvalidIbanException>();
    }

    [Test]
    public async Task OpenBankAccount_ShouldSendTheWelcomeLetter_WhenAccountIsOpened()
    {
        // Arrange

        var sender = GetService<ConfigurableWelcomeLetterSender>();

        // Act

        await Sender.Send(
            new OpenBankAccountCommand(
                Iban: "FR7630006000011234567890189",
                Currency: "EUR"
            )
        );

        // Assert

        sender.Sent.Should().ContainSingle()
            .Which.Value.Should().Be("FR7630006000011234567890189");
    }
}
