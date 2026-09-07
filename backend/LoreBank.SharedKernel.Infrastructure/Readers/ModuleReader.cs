using LoreBank.SharedKernel.Infrastructure.Persistence;

namespace LoreBank.SharedKernel.Infrastructure.Readers;

// La base des readers d'un module : une lecture requête une row keyless — le
// miroir plat d'une table, enregistré HasNoKey + ToView dans le modèle du
// module — et projette vers son Result dans le Select final, où EF ne lit que
// les colonnes touchées. Query est le seul point d'entrée : il refuse un type
// à clé ou hors modèle, ce qui rend mécanique la règle « une lecture ne
// matérialise jamais d'agrégat » (ModuleReaderTest épingle ce contrat).
public abstract class ModuleReader(ModuleDbContext context)
{
    protected IQueryable<TRow> Query<TRow>() where TRow : class
    {
        var entityType = context.Model.FindEntityType(typeof(TRow))
            ?? throw new InvalidOperationException(
                $"{typeof(TRow).Name} n'est pas dans le modèle de {context.GetType().Name} : "
                + "une row de lecture s'enregistre HasNoKey() + ToView(\"<table>\") "
                + "dans une IEntityTypeConfiguration du module."
            );

        if (entityType.FindPrimaryKey() is not null) {
            throw new InvalidOperationException(
                $"{typeof(TRow).Name} a une clé : un reader ne requête que des rows "
                + "keyless, jamais un agrégat — matérialiser pour muter est le rôle "
                + "du repository."
            );
        }

        return context.Set<TRow>();
    }
}
