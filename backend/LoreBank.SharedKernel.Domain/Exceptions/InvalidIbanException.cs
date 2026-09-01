namespace LoreBank.SharedKernel.Domain.Exceptions;

public sealed class InvalidIbanException(string value) : DomainException(
    new() { ["iban"] = value }
);
