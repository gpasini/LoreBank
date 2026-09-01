namespace LoreBank.SharedKernel.Domain.Exceptions;

public sealed class CurrencyMismatchException(
    string left,
    string right
) : DomainException(
    new() {
        ["left"] = left,
        ["right"] = right,
    }
);
