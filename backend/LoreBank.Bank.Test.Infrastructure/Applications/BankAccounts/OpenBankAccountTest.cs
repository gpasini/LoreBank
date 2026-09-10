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
    // use case. ResetFakes le remet sur Anonyme entre deux tests. Ce que le
    // Domain a enregistré se lit sur l'agrégat lui-même, par le DbSetup.
    [Test]
    public async Task OpenBankAccount_ShouldRecordTheActor_WhenSomeoneIsAuthenticated()
    {
        // Arrange

        GetService<ConfigurableCurrentActor>().Actor = Actor.Of("alice");

        // Act

        await DbSetup.CreateBankAccount().RunAsync();

        // Assert

        (await DbSetup.GetBankAccountAsync()).OpenedBy.Should().Be(Actor.Of("alice"));
    }

    // L'Instant est reçu, jamais demandé (ADR 0024) : le handler le demande à
    // TimeProvider — l'horloge du harnais ici — et le Domain enregistre ce
    // qu'on lui passe. ResetFakes efface l'Instant entre deux tests.
    [Test]
    public async Task OpenBankAccount_ShouldRecordTheInstant_WhenTheClockIsSet()
    {
        // Arrange

        var instant = new DateTimeOffset(
            year: 2026,
            month: 9,
            day: 9,
            hour: 8,
            minute: 30,
            second: 0,
            offset: TimeSpan.Zero
        );

        Factory.TimeProvider.Instant = instant;

        // Act

        await DbSetup.CreateBankAccount().RunAsync();

        // Assert

        (await DbSetup.GetBankAccountAsync()).OpenedAt.Should().Be(instant);
    }

    [Test]
    public async Task OpenBankAccount_ShouldRecordAnAnonymousActor_WhenNobodyIsAuthenticated()
    {
        // Act

        await DbSetup.CreateBankAccount().RunAsync();

        // Assert

        (await DbSetup.GetBankAccountAsync()).OpenedBy.Should().Be(Actor.Anonymous);
    }
}
