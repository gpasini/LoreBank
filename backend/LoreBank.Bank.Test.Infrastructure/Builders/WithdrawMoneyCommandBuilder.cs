using LoreBank.Bank.Application.Commands.WithdrawMoney;

namespace LoreBank.Bank.Test.Infrastructure.Builders;

public sealed class WithdrawMoneyCommandBuilder
{
    private decimal _amount = 10m;
    private string _currency = "EUR";

    public Guid? AccountId { get; private set; }

    public WithdrawMoneyCommandBuilder Of(Guid accountId)
    {
        AccountId = accountId;
        return this;
    }

    public WithdrawMoneyCommandBuilder WithAmount(decimal amount)
    {
        _amount = amount;
        return this;
    }

    public WithdrawMoneyCommandBuilder WithCurrency(string currency)
    {
        _currency = currency;
        return this;
    }

    public WithdrawMoneyCommand Build() => new(
        AccountId: AccountId ?? throw new InvalidOperationException("Le retrait n'a pas de compte : Of(accountId)."),
        Amount: _amount,
        Currency: _currency
    );
}
