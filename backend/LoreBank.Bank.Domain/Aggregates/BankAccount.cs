using LoreBank.Bank.Domain.Events;
using LoreBank.Bank.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Bank.Domain.Aggregates;

public sealed class BankAccount : AggregateRoot<BankAccountId>
{
    private BankAccount(
        BankAccountId id,
        Iban iban,
        Money balance
    ) : base(id)
    {
        Iban = iban;
        Balance = balance;
    }

    // Réservé à la matérialisation EF Core, qui écrit ensuite les backing fields.
    private BankAccount() : base(null!)
    {
        Iban = null!;
        Balance = null!;
    }

    public Iban Iban { get; }

    public Money Balance { get; private set; }

    public bool IsClosed { get; private set; }

    public static BankAccount Open(
        Iban iban,
        string currency
    )
    {
        var account = new BankAccount(
            id: BankAccountId.New(),
            iban: iban,
            balance: Money.Of(
                amount: 0m,
                currency: currency
            )
        );

        account.AddDomainEvent(
            new BankAccountOpenedDomainEvent(
                AccountId: account.Id,
                Iban: iban
            )
        );

        return account;
    }

    // PositiveMoney, pas Money : un dépôt négatif débiterait le compte en
    // contournant le contrôle de solde du retrait — le cas est inexprimable.
    public void Deposit(PositiveMoney amount)
    {
        EnsureIsOpen();
        Balance += amount.Value;
        AddDomainEvent(
            new MoneyDepositedDomainEvent(
                AccountId: Id,
                Amount: amount.Value,
                NewBalance: Balance
            )
        );
    }

    public void Withdraw(PositiveMoney amount)
    {
        EnsureIsOpen();

        if (amount.Value.Amount > Balance.Amount) {
            throw new InsufficientBalanceException(
                balance: Balance,
                requested: amount.Value
            );
        }

        Balance -= amount.Value;
        AddDomainEvent(
            new MoneyWithdrawnDomainEvent(
                AccountId: Id,
                Amount: amount.Value,
                NewBalance: Balance
            )
        );
    }

    public void Close()
    {
        EnsureIsOpen();

        if (Balance.Amount != 0m) {
            throw new NonEmptyAccountClosureException(
                accountId: Id,
                balance: Balance
            );
        }

        IsClosed = true;
        AddDomainEvent(new BankAccountClosedDomainEvent(Id));
    }

    private void EnsureIsOpen()
    {
        if (IsClosed) {
            throw new AccountClosedException(Id);
        }
    }
}
