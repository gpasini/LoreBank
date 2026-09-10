using LoreBank.Bank.Application.Commands.DepositMoney;

namespace LoreBank.Bank.Test.Infrastructure.Builders;

// Le prérequis (le compte) est nullable et lisible : « pas encore fourni »,
// que le geste du DbSetup comble depuis le dernier compte créé — jamais un
// Guid.Empty qui partirait vraiment dans la commande.
public sealed class DepositMoneyCommandBuilder
{
    private decimal _amount = 10m;
    private string _currency = "EUR";

    public Guid? AccountId { get; private set; }

    public DepositMoneyCommandBuilder Of(Guid accountId)
    {
        AccountId = accountId;
        return this;
    }

    public DepositMoneyCommandBuilder WithAmount(decimal amount)
    {
        _amount = amount;
        return this;
    }

    public DepositMoneyCommandBuilder WithCurrency(string currency)
    {
        _currency = currency;
        return this;
    }

    public DepositMoneyCommand Build() => new(
        AccountId: AccountId ?? throw new InvalidOperationException("Le dépôt n'a pas de compte : Of(accountId)."),
        Amount: _amount,
        Currency: _currency
    );
}
