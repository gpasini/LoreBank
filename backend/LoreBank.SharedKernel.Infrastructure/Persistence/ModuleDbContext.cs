using LoreBank.SharedKernel.Domain.Entities;
using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Infrastructure.Modules;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.SharedKernel.Infrastructure.Persistence;

// Le DbContext d'un module métier : porte le dispatch des domain events dans
// la transaction de la commande, pour que chaque module n'ait pas à recopier
// le bloc ramasse/vide/écrit/dispatch — la recopie était oubliable, et l'oubli
// silencieux. Porte aussi le schéma PostgreSQL du module, dérivé de l'identité
// comme les assemblies (ADR 0009) : un HasDefaultSchema oublié enverrait les
// tables du module dans public sans rien pour le signaler.
public abstract class ModuleDbContext : DbContext
{
    private readonly IDomainEventDispatcher _dispatcher;

    protected ModuleDbContext(
        DbContextOptions options,
        IDomainEventDispatcher dispatcher
    ) : base(options)
    {
        _dispatcher = dispatcher;
        Schema = ModuleAssemblyName
            .Parse(GetType().Assembly.GetName().Name ?? string.Empty)
            .Module
            .ToLowerInvariant();
    }

    // Seam interne réservé aux fakes de test (Sqlite) : leur assembly ne suit
    // pas la convention <Racine>.<Module>.Infrastructure dont la dérivation du
    // schéma se nourrit.
    internal ModuleDbContext(
        DbContextOptions options,
        IDomainEventDispatcher dispatcher,
        string schema
    ) : base(options)
    {
        _dispatcher = dispatcher;
        Schema = schema;
    }

    // « bank » pour LoreBank.Bank : le nom du module en minuscules. Un schéma
    // PostgreSQL par module est la précondition du partage d'une même base par
    // le harnais d'intégration (ADR 0002) ; les migrations de données et
    // l'outbox/inbox l'interpolent dans leur SQL via ModuleSql.
    public string Schema { get; }

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

        await _dispatcher.DispatchAsync(
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

    // La configuration commune est appliquée ici, où elle n'est pas oubliable :
    // le schéma du module, et ses IEntityTypeConfiguration cherchées dans
    // l'assembly du DbContext concret — plus de typeof à renommer au clonage.
    // Scellé pour que ConfigureModule, devenu optionnel, ne puisse pas la
    // contourner en oubliant d'appeler base.OnModelCreating.
    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        ConfigureModule(modelBuilder);
    }

    protected virtual void ConfigureModule(ModelBuilder modelBuilder)
    {
    }
}
