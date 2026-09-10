using LoreBank.Bank.Application.Commands.OpenBankAccount;

namespace LoreBank.Bank.Test.Infrastructure.Builders;

// Un builder par entrée du module (ADR 0030) : des défauts valides, une
// surcharge par propriété, et `Build()` rend la commande telle que le bord
// HTTP la poste. Il vit dans le Test.Infrastructure du module — jamais dans
// l'Application — et un module consommateur (Ledger) le réutilise par
// référence de test à test.
public sealed class OpenBankAccountCommandBuilder
{
    private string _iban = "FR7630006000011234567890189";
    private string _currency = "EUR";

    public OpenBankAccountCommandBuilder WithIban(string iban)
    {
        _iban = iban;
        return this;
    }

    public OpenBankAccountCommandBuilder WithCurrency(string currency)
    {
        _currency = currency;
        return this;
    }

    public OpenBankAccountCommand Build() => new(
        Iban: _iban,
        Currency: _currency
    );
}
