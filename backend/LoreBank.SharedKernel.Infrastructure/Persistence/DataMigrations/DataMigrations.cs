using System.Reflection;

namespace LoreBank.SharedKernel.Infrastructure.Persistence.DataMigrations;

public static class DataMigrations
{
    // Les migrations de données d'un module vivent dans l'assembly de son
    // DbContext — le même assembly que ses IEntityTypeConfiguration et ses
    // migrations EF. Le tri n'a pas lieu ici : c'est la timeline fusionnée qui
    // ordonne (ModuleMigrator), et ModuleCompositionTest rougit si une classe
    // découverte n'a pas d'id valide.
    public static IReadOnlyList<Type> DiscoverIn(Assembly assembly) => assembly
        .GetTypes()
        .Where(type => type is { IsAbstract: false } && type.IsAssignableTo(typeof(DataMigration)))
        .ToList();

    // « 20260904060000_NormalizeLegacyIbans » : le timestamp de l'attribut
    // puis le nom de la classe — la forme exacte des ids EF, pour que la
    // timeline fusionnée se trie avec un seul comparateur ordinal.
    public static string IdOf(Type migrationType)
    {
        var attribute = migrationType.GetCustomAttribute<DataMigrationAttribute>()
            ?? throw new InvalidOperationException(
                $"{migrationType.Name} ne porte pas [DataMigration] : sans timestamp, "
                + "la migration n'a pas de place dans la timeline."
            );

        return $"{attribute.Timestamp}_{migrationType.Name}";
    }
}
