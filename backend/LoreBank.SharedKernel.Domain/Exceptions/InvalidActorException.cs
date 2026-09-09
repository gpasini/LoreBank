namespace LoreBank.SharedKernel.Domain.Exceptions;

public sealed class InvalidActorException(string value) : DomainException(
    new() { ["actor"] = value }
);
