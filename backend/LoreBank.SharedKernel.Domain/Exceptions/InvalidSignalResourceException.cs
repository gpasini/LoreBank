namespace LoreBank.SharedKernel.Domain.Exceptions;

public sealed class InvalidSignalResourceException(string value) : DomainException(
    new() { ["resource"] = value }
);
