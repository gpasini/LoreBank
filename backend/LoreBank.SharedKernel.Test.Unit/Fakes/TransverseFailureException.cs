using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed class TransverseFailureException(
    string label,
    decimal amount
) : DomainException(
    new() {
        ["label"] = label,
        ["amount"] = amount,
    }
);
