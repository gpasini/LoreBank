using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LoreBank.SharedKernel.Infrastructure.Persistence;

// Le corps par défaut de ConfigureDbContext, publique pour qu'un module qui
// surcharge (autre nom de clé) n'ait pas à ré-encoder les invariants qu'elle
// concentre : la clé vit sous ConnectionStrings (la seule section que le
// harnais d'intégration redirige vers son conteneur — ADR 0002), une clé
// absente casse au démarrage plutôt qu'en null qui voyage jusqu'au premier
// SQL, Enlist=false est refusé (les connexions ne s'enrôleraient plus dans le
// TransactionScope ambiant, sans erreur ni test qui échoue), et le provider
// Npgsql est une décision du socle prise en un seul endroit (ADR 0008).
public static class ModuleDbContextRegistration
{
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName
    )
        where TContext : ModuleDbContext
    {
        // Validation immédiate : une clé absente ou une chaîne interdite casse
        // à la composition, pas à la première requête.
        GetValidConnectionString<TContext>(
            configuration: configuration,
            connectionStringName: connectionStringName
        );

        // Mais la valeur est relue à la fabrication des options : le harnais
        // d'intégration redirige les ConnectionStrings après la composition de
        // Program.cs — une valeur figée ici enverrait les tests sur la base
        // réelle du développeur (ConnectionRedirectTest épingle ce chemin).
        return services.AddDbContext<TContext>(
            options => options.UseNpgsql(GetValidConnectionString<TContext>(
                configuration: configuration,
                connectionStringName: connectionStringName
            ))
        );
    }

    private static string GetValidConnectionString<TContext>(
        IConfiguration configuration,
        string connectionStringName
    )
        where TContext : ModuleDbContext
    {
        var connectionString = configuration.GetConnectionString(connectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString)) {
            throw new InvalidOperationException(
                $"La chaîne de connexion « {connectionStringName} » est absente : le DbContext "
                + $"{typeof(TContext).Name} ne peut pas être monté. La clé doit vivre sous la section "
                + "ConnectionStrings — celle que le harnais d'intégration redirige vers son conteneur."
            );
        }

        if (!new NpgsqlConnectionStringBuilder(connectionString).Enlist) {
            throw new InvalidOperationException(
                $"La chaîne de connexion « {connectionStringName} » porte Enlist=false : les connexions ne "
                + "s'enrôleraient plus dans le TransactionScope ambiant du TransactionBehavior, et les commandes "
                + "cesseraient d'être transactionnelles sans erreur ni test qui échoue."
            );
        }

        return connectionString;
    }
}
