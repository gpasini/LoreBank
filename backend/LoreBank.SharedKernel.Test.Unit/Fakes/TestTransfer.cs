using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

// Le terrain du test d'aliasing des VO owned : deux propriétaires possibles
// (débit, crédit) pour des instances de Money — la géométrie minimale où le
// partage d'une même instance entre deux owners peut se produire.
public sealed class TestTransfer
{
    public Guid Id { get; set; }

    public Money Debit { get; set; } = null!;

    public Money Credit { get; set; } = null!;
}
