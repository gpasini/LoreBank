using LoreBank.Bank.Contracts.Readers;
using LoreBank.Bank.Test.Infrastructure.Setups;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.Bank.Test.Infrastructure.Applications.BankAccounts;

// Le port de lecture publié est un reader comme les autres : il lit une row
// keyless (ADR 0018) dont le mapping est en chaînes que rien ne compile, et
// ce test est le seul lien entre ses colonnes et les propriétés du DTO — il
// relit chaque champ après écriture.
public sealed class BankAccountsContractReaderTest : BaseIntegrationTest<BankWebAppFactory, DbSetup>
{
    // Pour un lecteur, l'absence est un résultat normal : c'est le module
    // consommateur qui décide de ce qu'elle signifie chez lui.
    [Test]
    public async Task FindById_ShouldReturnNull_WhenAccountIsUnknown()
    {
        var summary = await GetService<IBankAccountsContract>().FindByIdAsync(
            accountId: Guid.NewGuid(),
            cancellationToken: CancellationToken.None
        );

        summary.Should().BeNull();
    }

    [Test]
    public async Task FindById_ShouldMapEveryColumn_WhenAccountExists()
    {
        // Arrange

        await DbSetup.CreateBankAccount(account => account.WithIban("FR7630006000011234567890189")).RunAsync();

        var accountId = DbSetup.GetLastBankAccountId();

        // Act

        var summary = await GetService<IBankAccountsContract>().FindByIdAsync(
            accountId: accountId.Value,
            cancellationToken: CancellationToken.None
        );

        // Assert

        summary.Should().NotBeNull();
        summary.Id.Should().Be(accountId.Value);
        summary.Iban.Should().Be("FR7630006000011234567890189");
    }
}
