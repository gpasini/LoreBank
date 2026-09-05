using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Ledger.Domain.Exceptions;

public sealed class UnbalancedJournalEntryException(
    Money debits,
    Money credits
) : DomainException(
    new() {
        ["debits"] = debits.Amount,
        ["credits"] = credits.Amount,
        ["currency"] = debits.Currency,
    }
);
