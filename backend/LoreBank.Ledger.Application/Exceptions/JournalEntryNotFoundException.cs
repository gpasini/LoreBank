using LoreBank.Ledger.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.Ledger.Application.Exceptions;

public sealed class JournalEntryNotFoundException(JournalEntryId entryId) : NotFoundException(
    new() { ["entryId"] = entryId.Value }
);
