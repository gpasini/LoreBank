using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.Ledger.Domain.Exceptions;

public sealed class EmptyJournalEntryException() : DomainException(new());
