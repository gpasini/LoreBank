using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.Probe.Infrastructure.Persistence;

// Le schéma (« probe ») et les IEntityTypeConfiguration de cette assembly
// sont appliqués par la base, exactement comme pour un module métier
// (ADR 0009) : le terrain emprunte le vrai chemin, il ne le simule pas.
public sealed class ProbeDbContext(
    DbContextOptions<ProbeDbContext> options,
    IDomainEventDispatcher dispatcher
) : ModuleDbContext(
    options: options,
    dispatcher: dispatcher
)
{
    public DbSet<ProbeThing> ProbeThings => Set<ProbeThing>();
}
