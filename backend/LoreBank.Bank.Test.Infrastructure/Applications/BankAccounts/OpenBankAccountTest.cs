using LoreBank.Bank.Application.Commands.OpenBankAccount;
using LoreBank.Bank.Application.Queries.GetBankAccountById;
using LoreBank.Bank.Test.Infrastructure.Fakes;
using LoreBank.Bank.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.ValueObjects;

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

    // « Agir en tant que » (ADR 0023) : le test pose l'Acteur sur le fake du
    // port avant l'arrange — un contexte de la requête, pas un paramètre du
    // use case. ResetFakes le remet sur Anonyme entre deux tests.
    [Test]
    public async Task OpenBankAccount_ShouldRecordTheActor_WhenSomeoneIsAuthenticated()
    {
        // Arrange

        GetService<ConfigurableCurrentActor>().Actor = Actor.Of("alice");

        await DbSetup.CreateBankAccountAsync();

        // Act

        var account = await Sender.Send(new GetBankAccountByIdQuery(DbSetup.GetLastBankAccountId().Value));

        // Assert

        account.OpenedBy.Should().Be("alice");
    }

    [Test]
    public async Task OpenBankAccount_ShouldRecordAnAnonymousActor_WhenNobodyIsAuthenticated()
    {
        // Arrange

        await DbSetup.CreateBankAccountAsync();

        // Act

        var account = await Sender.Send(new GetBankAccountByIdQuery(DbSetup.GetLastBankAccountId().Value));

        // Assert

        account.OpenedBy.Should().BeNull();
    }
}
