using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed class NullFailureException() : DomainException(
    new() { ["label"] = null! }
);
