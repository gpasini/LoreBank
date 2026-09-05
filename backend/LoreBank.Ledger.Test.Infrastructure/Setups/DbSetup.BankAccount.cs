using LoreBank.Bank.Application.Commands.OpenBankAccount;

namespace LoreBank.Ledger.Test.Infrastructure.Setups;

// Les comptes servent d'ancrage au port de lecture publié par Bank : ils se
// créent par les vrais use cases de Bank — un projet de test n'est pas tenu
// par la frontière des Contrats, c'est l'hôte entier qu'il exerce.
public partial class DbSetup
{
    private readonly List<Guid> _bankAccountIds = [];

    public async Task CreateBankAccountAsync(string iban = "FR7630006000011234567890189")
    {
        var accountId = await Sender.Send(
            new OpenBankAccountCommand(
                Iban: iban,
                Currency: "EUR"
            )
        );

        _bankAccountIds.Add(accountId);
    }

    public Guid GetLastBankAccountId() => _bankAccountIds.Last();
}
