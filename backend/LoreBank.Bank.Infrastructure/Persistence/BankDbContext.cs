using LoreBank.Bank.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.Bank.Infrastructure.Persistence;

// Le schéma (« bank ») et les IEntityTypeConfiguration de cette assembly sont
// appliqués par la base — rien à configurer ici (ADR 0009).
public sealed class BankDbContext(
    DbContextOptions<BankDbContext> options,
    IDomainEventDispatcher dispatcher
) : ModuleDbContext(
    options: options,
    dispatcher: dispatcher
)
{
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
}
