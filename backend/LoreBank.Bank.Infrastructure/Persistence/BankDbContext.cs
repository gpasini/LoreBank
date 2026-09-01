using LoreBank.Bank.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.Entities;
using LoreBank.SharedKernel.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.Bank.Infrastructure.Persistence;

public sealed class BankDbContext(
    DbContextOptions<BankDbContext> options,
    IDomainEventDispatcher dispatcher
) : DbContext(options)
{
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();

    // Les events sont vidés des entités avant l'écriture, pour qu'un handler qui
    // sauvegarde à son tour ne les redispatche pas. Le dispatch a lieu après
    // l'écriture mais avant le commit : un handler qui échoue annule la commande.
    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        var domainEvents = ChangeTracker.Entries()
            .Select(entry => entry.Entity)
            .OfType<IHasDomainEvents>()
            .SelectMany(entity => {
                var events = entity.DomainEvents.ToList();
                entity.ClearDomainEvents();

                return events;
            })
            .ToList();

        var result = await base.SaveChangesAsync(
            acceptAllChangesOnSuccess: acceptAllChangesOnSuccess,
            cancellationToken: cancellationToken
        );

        await dispatcher.DispatchAsync(
            domainEvents: domainEvents,
            cancellationToken: cancellationToken
        );

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("bank");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BankDbContext).Assembly);
    }
}
