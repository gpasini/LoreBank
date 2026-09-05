using LoreBank.Ledger.Application.Exceptions;
using LoreBank.Ledger.Domain.Aggregates;
using LoreBank.Ledger.Domain.Repositories;
using LoreBank.Ledger.Infrastructure.Persistence;
using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Infrastructure.Repositories;

namespace LoreBank.Ledger.Infrastructure.Repositories;

// Chargement, mini-unit-of-work et dispatch des events vivent dans
// ModuleRepository — ne reste que ce que la base ne peut pas savoir.
public sealed class JournalEntryRepository(LedgerDbContext context)
    : ModuleRepository<JournalEntry, JournalEntryId>(context), IJournalEntryRepository
{
    protected override NotFoundException NotFound(JournalEntryId id) => new JournalEntryNotFoundException(id);
}
