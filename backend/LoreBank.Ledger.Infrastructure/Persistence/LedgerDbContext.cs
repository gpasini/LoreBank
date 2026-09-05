using LoreBank.Ledger.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.Ledger.Infrastructure.Persistence;

// Le schéma (« ledger ») et les IEntityTypeConfiguration de cette assembly
// sont appliqués par la base — rien à configurer ici (ADR 0009).
public sealed class LedgerDbContext(
    DbContextOptions<LedgerDbContext> options,
    IDomainEventDispatcher dispatcher
) : ModuleDbContext(
    options: options,
    dispatcher: dispatcher
)
{
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
}
