using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.Ledger.Domain.Exceptions;

public sealed class InvalidLedgerAccountRefException(string value) : DomainException(
    new() { ["value"] = value }
);
