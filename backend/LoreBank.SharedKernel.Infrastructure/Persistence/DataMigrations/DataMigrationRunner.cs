using Autofac;
using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Infrastructure.Persistence.DataMigrations;

// Le bras « données » de ModuleMigrator : un runner par module, propriétaire
// du scope dans lequel les migrations de données s'exécutent. Trois garanties
// vivent ici plutôt qu'en recopie par migration : le dispatcher d'events du
// scope est neutre (une migration ne produit aucun fait métier — ADR 0013),
// chaque migration s'applique dans sa propre transaction, et la ligne de
// journal part dans cette même transaction — jamais appliquée sans être
// journalisée, ni journalisée sans être appliquée.
public sealed class DataMigrationRunner : IAsyncDisposable
{
    private readonly ILifetimeScope _scope;

    private DataMigrationRunner(
        ILifetimeScope scope,
        ModuleDbContext dbContext
    )
    {
        _scope = scope;
        DbContext = dbContext;
    }

    // Exposé pour que ModuleMigrator applique les pas de schéma sur le même
    // DbContext que les pas de données — une seule connexion, une seule
    // timeline.
    public ModuleDbContext DbContext { get; }

    public static DataMigrationRunner Create(
        IServiceProvider services,
        Type dbContextType
    )
    {
        // Un scope enfant Autofac où IDomainEventDispatcher est substitué : le
        // DbContext résolu dedans — et tout ce qu'une migration se fait
        // injecter — ne dispatche rien. La substitution vit dans le scope, pas
        // dans un flag du DbContext : aucun état à remettre, rien à oublier.
        var scope = services
            .GetRequiredService<ILifetimeScope>()
            .BeginLifetimeScope(builder => builder
                .RegisterType<NoOpDomainEventDispatcher>()
                .As<IDomainEventDispatcher>()
                .InstancePerLifetimeScope()
            );

        return new DataMigrationRunner(
            scope: scope,
            dbContext: (ModuleDbContext)scope.Resolve(dbContextType)
        );
    }

    // Le journal est créé paresseusement au premier passage : sur une base
    // neuve, il peut précéder la première migration EF — d'où le CREATE SCHEMA.
    public async Task<IReadOnlyList<string>> GetAppliedIdsAsync(CancellationToken cancellationToken)
    {
        await ExecuteSqlAsync(
            sql: $"""
                  CREATE SCHEMA IF NOT EXISTS {DbContext.Schema};
                  CREATE TABLE IF NOT EXISTS {JournalTable} (
                      migration_id character varying(300) NOT NULL PRIMARY KEY,
                      applied_at timestamp with time zone NOT NULL
                  );
                  """,
            parameters: new Dictionary<string, object>(),
            cancellationToken: cancellationToken
        );

        return await ModuleSql.ExecuteAsync(
            dbContext: DbContext,
            sql: $"SELECT migration_id FROM {JournalTable} ORDER BY migration_id",
            parameters: new Dictionary<string, object>(),
            execute: async (
                command,
                token
            ) =>
            {
                var ids = new List<string>();

                await using var reader = await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token)) {
                    ids.Add(reader.GetString(0));
                }

                return (IReadOnlyList<string>)ids;
            },
            cancellationToken: cancellationToken
        );
    }

    // Tout-ou-rien : la migration et sa ligne de journal partagent une
    // transaction. Un échec laisse la base au dernier pas commité de la
    // timeline — un état cohérent — et le run suivant reprend exactement ici,
    // le journal faisant foi.
    public async Task ApplyAsync(
        Type migrationType,
        CancellationToken cancellationToken
    )
    {
        // Les paramètres du constructeur sont résolus dans le scope du runner :
        // le DbContext injecté est LA même instance que celle du runner — la
        // migration écrit dans la transaction ouverte ci-dessous, pas à côté.
        var constructor = migrationType.GetConstructors().Single();
        var migration = (DataMigration)constructor.Invoke(
            constructor
                .GetParameters()
                .Select(parameter => _scope.Resolve(parameter.ParameterType))
                .ToArray()
        );

        await using var transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken);

        await migration.ExecuteAsync(cancellationToken);

        await ExecuteSqlAsync(
            sql: $"INSERT INTO {JournalTable} (migration_id, applied_at) VALUES (@id, now())",
            parameters: new Dictionary<string, object> {
                ["id"] = DataMigrations.IdOf(migrationType),
            },
            cancellationToken: cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    private string JournalTable => $"{DbContext.Schema}.__data_migrations_history";

    // Le geste SQL — emprunt, finally, paramètres, enrôlement dans la
    // transaction courante — vit dans ModuleSql. Le schéma interpolé est
    // dérivé de l'identité (ADR 0009), jamais une saisie.
    private Task ExecuteSqlAsync(
        string sql,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken
    ) => ModuleSql.ExecuteNonQueryAsync(
        dbContext: DbContext,
        sql: sql,
        parameters: parameters,
        cancellationToken: cancellationToken
    );
}
