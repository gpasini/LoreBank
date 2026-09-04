using LoreBank.SharedKernel.Domain.Entities;
using LoreBank.SharedKernel.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.SharedKernel.Infrastructure.Persistence;

// Le DbContext d'un module métier : porte le dispatch des domain events dans
// la transaction de la commande, pour que chaque module n'ait pas à recopier
// le bloc ramasse/vide/écrit/dispatch — la recopie était oubliable, et l'oubli
// silencieux.
public abstract class ModuleDbContext(
    DbContextOptions options,
    IDomainEventDispatcher dispatcher
) : DbContext(options)
{
    // Les events sont vidés des entités avant l'écriture, pour qu'un handler qui
    // sauvegarde à son tour ne les redispatche pas. Le dispatch a lieu après
    // l'écriture mais avant le commit : un handler qui échoue annule la commande.
    public sealed override async Task<int> SaveChangesAsync(
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

    // Les handlers sont async : les dispatcher d'ici obligerait à bloquer.
    // Plutôt qu'un dispatch perdu en silence ou un sync-over-async, la famille
    // synchrone est interdite. SaveChanges() délègue à cette surcharge.
    public sealed override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException(
            "SaveChanges synchrone perdrait les domain events : utiliser SaveChangesAsync."
        );

    // Scellé pour que la configuration du modèle passe toujours par le hook :
    // si la base a un jour une convention à imposer, aucun module ne peut
    // l'avoir contournée en oubliant d'appeler base.OnModelCreating.
    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureModule(modelBuilder);
    }

    protected abstract void ConfigureModule(ModelBuilder modelBuilder);
}
