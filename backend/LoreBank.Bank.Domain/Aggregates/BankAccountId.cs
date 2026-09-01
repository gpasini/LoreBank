using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Bank.Domain.Aggregates;

public sealed class BankAccountId(Guid value) : SimpleValueObject<Guid>(value)
{
    public static BankAccountId New() => new(Guid.NewGuid());
}
