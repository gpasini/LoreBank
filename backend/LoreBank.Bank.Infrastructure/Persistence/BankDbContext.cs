using LoreBank.Bank.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.Bank.Infrastructure.Persistence;

public sealed class BankDbContext(
    DbContextOptions<BankDbContext> options,
    IDomainEventDispatcher dispatcher
) : ModuleDbContext(
    options: options,
    dispatcher: dispatcher
)
{
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("bank");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BankDbContext).Assembly);
    }
}
