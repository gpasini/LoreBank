namespace LoreBank.Ledger.Test.Unit.Fakes;

// L'horloge des tests unitaires d'Application (ADR 0024) : un Instant fixe,
// rien d'autre — le Domain, lui, reçoit une valeur et n'a besoin d'aucun fake.
public sealed class FixedTimeProvider(DateTimeOffset instant) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => instant;
}
