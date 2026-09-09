using LoreBank.SharedKernel.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.Entities;
using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Infrastructure.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LoreBank.SharedKernel.Infrastructure.Persistence;

// Le DbContext d'un module métier : porte le dispatch des domain events dans
// la transaction de la commande, pour que chaque module n'ait pas à recopier
// le bloc ramasse/vide/écrit/dispatch — la recopie était oubliable, et l'oubli
// silencieux. Porte aussi le schéma PostgreSQL du module, dérivé de l'identité
// comme les assemblies (ADR 0009) : un HasDefaultSchema oublié enverrait les
// tables du module dans public sans rien pour le signaler. Porte enfin la
// Version d'agrégat (ADR 0020) : une propriété shadow déclarée jeton de
// concurrence sur tout AggregateRoot du modèle, incrémentée à chaque
// sauvegarde qui touche l'agrégat — un module n'a rien à déclarer et ne peut
// pas l'oublier — et la traduction de l'échec EF en ConcurrentUpdateException.
public abstract class ModuleDbContext : DbContext
{
    // Le nom de la propriété shadow — le Domain ne la voit pas, les tests et
    // les migrations la nomment par cette constante.
    public const string VersionPropertyName = "Version";

    public const string VersionColumnName = "version";

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
    // Une écriture refusée pour version périmée sort avant le dispatch : rien
    // ne part, ni event ni ligne d'outbox.
    public override sealed async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        BumpAggregateVersions();

        var domainEvents = ChangeTracker.Entries()
            .Select(entry => entry.Entity)
            .OfType<IHasDomainEvents>()
            .SelectMany(entity =>
            {
                var events = entity.DomainEvents.ToList();
                entity.ClearDomainEvents();

                return events;
            })
            .ToList();

        int result;

        try {
            result = await base.SaveChangesAsync(
                acceptAllChangesOnSuccess: acceptAllChangesOnSuccess,
                cancellationToken: cancellationToken
            );
        } catch (DbUpdateConcurrencyException exception) {
            throw new ConcurrentUpdateException(IdOf(exception.Entries));
        }

        await _dispatcher.DispatchAsync(
            domainEvents: domainEvents,
            cancellationToken: cancellationToken
        );

        return result;
    }

    // Les handlers sont async : les dispatcher d'ici obligerait à bloquer.
    // Plutôt qu'un dispatch perdu en silence ou un sync-over-async, la famille
    // synchrone est interdite. SaveChanges() délègue à cette surcharge.
    public override sealed int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException(
            "SaveChanges synchrone perdrait les domain events : utiliser SaveChangesAsync."
        );

    // La configuration commune est appliquée ici, où elle n'est pas oubliable :
    // le schéma du module, et ses IEntityTypeConfiguration cherchées dans
    // l'assembly du DbContext concret — plus de typeof à renommer au clonage.
    // Scellé pour que ConfigureModule, devenu optionnel, ne puisse pas la
    // contourner en oubliant d'appeler base.OnModelCreating.
    protected override sealed void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        ConfigureModule(modelBuilder);
        DeclareAggregateVersions(modelBuilder);
    }

    protected virtual void ConfigureModule(ModelBuilder modelBuilder)
    {
    }

    // Après les configurations du module et ConfigureModule : tout ce qui est
    // dans le modèle et dérive AggregateRoot<> reçoit sa Version. Pas
    // d'opt-out — un agrégat append-only porte une colonne à zéro, c'est
    // moins cher qu'un oubli.
    private static void DeclareAggregateVersions(ModelBuilder modelBuilder)
    {
        var aggregates = modelBuilder.Model
            .GetEntityTypes()
            .Where(entityType => !entityType.IsOwned() && IsAggregateRoot(entityType.ClrType))
            .Select(entityType => entityType.ClrType)
            .ToList();

        foreach (var aggregate in aggregates) {
            modelBuilder
                .Entity(aggregate)
                .Property<int>(VersionPropertyName)
                .HasColumnName(VersionColumnName)
                .IsConcurrencyToken();
        }
    }

    // Une racine dont l'entrée est modifiée, ou dont une dépendance owned est
    // ajoutée, modifiée ou supprimée, voit sa Version incrémentée. Le second
    // cas est le piège : remplacer l'instance d'un VO owned (le solde d'un
    // compte) laisse la racine Unchanged aux yeux d'EF. Une racine ajoutée
    // part à zéro, une racine supprimée n'a plus de version à défendre.
    private void BumpAggregateVersions()
    {
        ChangeTracker.DetectChanges();

        var autoDetectChanges = ChangeTracker.AutoDetectChangesEnabled;
        ChangeTracker.AutoDetectChangesEnabled = false;

        try {
            var roots = ChangeTracker.Entries()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(RootOf)
                .OfType<EntityEntry>()
                .Where(root => root.State is EntityState.Unchanged or EntityState.Modified)
                .Where(root => IsAggregateRoot(root.Metadata.ClrType))
                .DistinctBy(
                    keySelector: root => root.Entity,
                    comparer: ReferenceEqualityComparer.Instance
                )
                .ToList();

            foreach (var root in roots) {
                var version = root.Property(VersionPropertyName);
                version.CurrentValue = (int) version.OriginalValue! + 1;
            }
        } finally {
            ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
        }
    }

    // Remonte la chaîne de possession d'une entrée jusqu'à son entité non
    // owned : le propriétaire est l'entrée suivie dont la clé porte les
    // valeurs de la clé étrangère de possession. Null si le propriétaire
    // n'est pas suivi — rien à incrémenter alors.
    private EntityEntry? RootOf(EntityEntry entry)
    {
        var current = entry;

        while (current.Metadata.IsOwned()) {
            var ownership = current.Metadata.FindOwnership()!;
            var foreignKeyValues = ownership.Properties
                .Select(property => current.Property(property.Name).CurrentValue)
                .ToArray();

            var owner = ChangeTracker.Entries().FirstOrDefault(candidate =>
                candidate.Metadata == ownership.PrincipalEntityType
                && ownership.PrincipalKey.Properties
                    .Select((
                            property,
                            index
                        ) => Equals(
                            objA: candidate.Property(property.Name).CurrentValue,
                            objB: foreignKeyValues[index]
                        )
                    )
                    .All(matches => matches)
            );

            if (owner is null) {
                return null;
            }

            current = owner;
        }

        return current;
    }

    // La clé de la racine refusée, en valeur provider : le paramètre d'une
    // DomainException est une primitive, jamais un VO. Les entrées de
    // l'exception peuvent être celles d'un VO owned partageant la table de
    // la racine — on remonte à la racine avant de lire la clé.
    private object IdOf(IReadOnlyList<EntityEntry> entries)
    {
        var root = entries.Select(RootOf).OfType<EntityEntry>().First();
        var key = root.Metadata.FindPrimaryKey()!.Properties[0];
        var value = root.Property(key.Name).CurrentValue!;
        var converter = key.FindTypeMapping()?.Converter ?? key.GetValueConverter();

        return converter?.ConvertToProvider(value) ?? value;
    }

    private static bool IsAggregateRoot(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType) {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AggregateRoot<>)) {
                return true;
            }
        }

        return false;
    }
}
