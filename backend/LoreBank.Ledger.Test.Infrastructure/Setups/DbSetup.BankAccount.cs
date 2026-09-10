using LoreBank.Bank.Test.Infrastructure.Builders;

namespace LoreBank.Ledger.Test.Infrastructure.Setups;

// Les comptes servent d'ancrage au port de lecture publié par Bank : ils se
// créent par les vrais use cases de Bank — un projet de test n'est pas tenu
// par la frontière des Contrats, c'est l'hôte entier qu'il exerce — avec le
// builder de commande de Bank, réutilisé par référence de test à test
// (ADR 0030). Le geste reste celui de Ledger : de Bank, il ne garde que
// l'id.
public partial class DbSetup
{
    private readonly List<Guid> _bankAccountIds = [];

    public DbSetup CreateBankAccount(Action<OpenBankAccountCommandBuilder>? configure = null)
    {
        Enqueue(
            name: nameof(CreateBankAccount),
            step: () => OpenAsync(configure)
        );

        return this;
    }

    public Guid GetLastBankAccountId() => Arranged(() => _bankAccountIds.Last());

    private async Task OpenAsync(Action<OpenBankAccountCommandBuilder>? configure)
    {
        var builder = new OpenBankAccountCommandBuilder();

        configure?.Invoke(builder);

        _bankAccountIds.Add(await Sender.Send(builder.Build()));
    }

    private async Task<Guid> LastOrNewAccountIdAsync()
    {
        if (_bankAccountIds.Count == 0) {
            await OpenAsync(configure: null);
        }

        return _bankAccountIds.Last();
    }
}
