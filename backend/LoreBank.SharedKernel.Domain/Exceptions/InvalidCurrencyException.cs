namespace LoreBank.SharedKernel.Domain.Exceptions;

public sealed class InvalidCurrencyException(string value) : DomainException(
    new() { ["currency"] = value }
);
