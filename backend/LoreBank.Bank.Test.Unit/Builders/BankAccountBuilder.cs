using LoreBank.Bank.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Bank.Test.Unit.Builders;

public sealed class BankAccountBuilder
{
    private Iban _iban = Iban.Parse("FR7630006000011234567890189");
    private string _currency = "EUR";
    private Actor _openedBy = Actor.Anonymous;
    private decimal _balance;
    private bool _isClosed;

    public BankAccountBuilder WithIban(Iban iban)
    {
        _iban = iban;
        return this;
    }

    public BankAccountBuilder WithCurrency(string currency)
    {
        _currency = currency;
        return this;
    }

    public BankAccountBuilder OpenedBy(Actor actor)
    {
        _openedBy = actor;
        return this;
    }

    public BankAccountBuilder WithBalance(decimal balance)
    {
        _balance = balance;
        return this;
    }

    public BankAccountBuilder Closed()
    {
        _isClosed = true;
        return this;
    }

    public BankAccount Build()
    {
        var account = BankAccount.Open(
            iban: _iban,
            currency: _currency,
            openedBy: _openedBy
        );

        if (_balance > 0m) {
            account.Deposit(
                PositiveMoney.Of(
                    amount: _balance,
                    currency: _currency
                )
            );
        }

        if (_isClosed) {
            if (_balance > 0m) {
                account.Withdraw(
                    PositiveMoney.Of(
                        amount: _balance,
                        currency: _currency
                    )
                );
            }

            account.Close();
        }

        account.ClearDomainEvents();

        return account;
    }
}
