namespace LoreBank.SharedKernel.Domain.Exceptions;

public sealed class NonPositiveAmountException(
    decimal amount,
    string currency
) : DomainException(
    new() {
        ["amount"] = amount,
        ["currency"] = currency,
    }
);
