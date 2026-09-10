using LoreBank.Bank.Contracts.IntegrationEvents;

namespace LoreBank.Ledger.Test.Infrastructure.Builders;

public sealed class MoneyWithdrawnIntegrationEventBuilder
{
    private decimal _amount = 10m;
    private string _currency = "EUR";

    public Guid? AccountId { get; private set; }

    public MoneyWithdrawnIntegrationEventBuilder Of(Guid accountId)
    {
        AccountId = accountId;
        return this;
    }

    public MoneyWithdrawnIntegrationEventBuilder WithAmount(decimal amount)
    {
        _amount = amount;
        return this;
    }

    public MoneyWithdrawnIntegrationEventBuilder WithCurrency(string currency)
    {
        _currency = currency;
        return this;
    }

    public MoneyWithdrawnIntegrationEvent Build() => new(
        AccountId: AccountId ?? throw new InvalidOperationException("Le retrait n'a pas de compte : Of(accountId)."),
        Amount: _amount,
        Currency: _currency
    );
}
