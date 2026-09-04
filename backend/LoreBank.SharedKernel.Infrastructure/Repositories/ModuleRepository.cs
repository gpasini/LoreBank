using LoreBank.SharedKernel.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.SharedKernel.Infrastructure.Repositories;

// La base des repositories d'agrégats d'un module : un repository concret ne
// fournit que sa fabrique de NotFoundException — le port du Domain reste écrit
// par le module, c'est son contrat métier. Deux invariants vivent ici plutôt
// qu'en recopie par agrégat : le mini-unit-of-work (ajouter l'agrégat au
// tracker s'il est détaché, écrire sinon), et surtout le fait que SaveAsync
// passe par SaveChangesAsync de ModuleDbContext — c'est LÀ que les domain
// events sont dispatchés (ADR 0003) : un repository maison qui écrirait
// autrement casserait la chaîne en silence.
public abstract class ModuleRepository<TAggregate, TId>(ModuleDbContext context)
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    // Non nullable : l'absence d'un agrégat chargé pour être muté est une
    // erreur métier, portée par la NotFoundException du module — le handler
    // n'a plus de « ?? throw » à recopier. FindAsync passe par le change
    // tracker puis la clé primaire : la sémantique d'un chargement d'agrégat.
    public async Task<TAggregate> GetRequiredByIdAsync(
        TId id,
        CancellationToken cancellationToken
    ) => await context.Set<TAggregate>().FindAsync(
        keyValues: [id],
        cancellationToken: cancellationToken
    ) ?? throw NotFound(id);

    public async Task SaveAsync(
        TAggregate aggregate,
        CancellationToken cancellationToken
    )
    {
        if (context.Entry(aggregate).State == EntityState.Detached) {
            context.Set<TAggregate>().Add(aggregate);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    protected abstract NotFoundException NotFound(TId id);
}
