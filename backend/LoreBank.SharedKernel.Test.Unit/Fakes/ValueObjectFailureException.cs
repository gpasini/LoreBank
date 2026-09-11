using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

// Le contre-exemple de docs/erreurs.md : le VO passé entier, au lieu de ses
// primitives.
public sealed class ValueObjectFailureException(Money balance) : DomainException(
    new() { ["balance"] = balance }
);
