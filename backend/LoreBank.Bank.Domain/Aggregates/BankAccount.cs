using LoreBank.Bank.Domain.Events;
using LoreBank.Bank.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Bank.Domain.Aggregates;

public sealed class BankAccount : AggregateRoot<BankAccountId>
{
    // L'Anonyme est null hors du Domain (ADR 0023) : ce champ est la seule
    // place où le Domain le voit — EF matérialise la colonne nullable dedans,
    // sans convertisseur à appeler sur un NULL, et OpenedBy le retraduit.
    private readonly Actor? _openedBy;

    private BankAccount(
        BankAccountId id,
        Iban iban,
        Money balance,
        Actor openedBy,
        DateTimeOffset openedAt
    ) : base(id)
    {
        Iban = iban;
        Balance = balance;
        _openedBy = openedBy.IsAnonymous ? null : openedBy;
        OpenedAt = openedAt;
    }

    // Réservé à la matérialisation EF Core, qui écrit ensuite les backing fields.
    private BankAccount() : base(null!)
    {
        Iban = null!;
        Balance = null!;
    }

    public Iban Iban { get; }

    // L'Acteur qui a ouvert le compte (ADR 0023) : reçu de la transition,
    // jamais demandé — Anonyme tant que personne n'authentifie.
    public Actor OpenedBy => _openedBy ?? Actor.Anonymous;

    // L'Instant de l'ouverture (ADR 0024) : reçu de la transition, jamais
    // demandé — le Domain ne lit pas l'horloge.
    public DateTimeOffset OpenedAt { get; }

    public Money Balance { get; private set; }

    public bool IsClosed { get; private set; }

    public static BankAccount Open(
        Iban iban,
        string currency,
        Actor openedBy,
        DateTimeOffset openedAt
    )
    {
        var account = new BankAccount(
            id: BankAccountId.New(),
            iban: iban,
            balance: Money.Of(
                amount: 0m,
                currency: currency
            ),
            openedBy: openedBy,
            openedAt: openedAt
        );

        account.AddDomainEvent(
            new BankAccountOpenedDomainEvent(
                AccountId: account.Id,
                Iban: iban,
                OpenedBy: openedBy,
                OpenedAt: openedAt
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
