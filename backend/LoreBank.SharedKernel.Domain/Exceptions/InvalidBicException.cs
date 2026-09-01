namespace LoreBank.SharedKernel.Domain.Exceptions;

public sealed class InvalidBicException(string value) : DomainException(
    new() { ["bic"] = value }
);
