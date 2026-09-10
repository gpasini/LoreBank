using LoreBank.Bank.Contracts.IntegrationEvents;

namespace LoreBank.Ledger.Test.Infrastructure.Builders;

// Un builder par entrée du module (ADR 0030) : les entrées d'un consommateur
// sont les integration events qu'il consomme — le builder vit chez lui, pas
// chez le publieur. Le compte est le prérequis, nullable et lisible, que le
// geste du DbSetup comble depuis le dernier compte créé.
public sealed class MoneyDepositedIntegrationEventBuilder
{
    private decimal _amount = 10m;
    private string _currency = "EUR";

    public Guid? AccountId { get; private set; }

    public MoneyDepositedIntegrationEventBuilder Of(Guid accountId)
    {
        AccountId = accountId;
        return this;
    }

    public MoneyDepositedIntegrationEventBuilder WithAmount(decimal amount)
    {
        _amount = amount;
        return this;
    }

    public MoneyDepositedIntegrationEventBuilder WithCurrency(string currency)
    {
        _currency = currency;
        return this;
    }

    public MoneyDepositedIntegrationEvent Build() => new(
        AccountId: AccountId ?? throw new InvalidOperationException("Le dépôt n'a pas de compte : Of(accountId)."),
        Amount: _amount,
        Currency: _currency
    );
}
