using LoreBank.Probe.Infrastructure.Persistence;
using LoreBank.Probe.Infrastructure.Persistence.ReadRows;
using LoreBank.SharedKernel.Application;
using LoreBank.SharedKernel.Infrastructure.Readers;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// Le reader-sonde de Liste : la déclaration complète que le moteur sait
// exécuter — recherche sur le libellé, deux filtres facettés, tri — et rien
// d'autre : ni Skip/Take, ni COUNT, ni GROUP BY (ADR 0027).
public sealed class ProbeThingReader(ProbeDbContext context) : ModuleReader(context)
{
    public Task<ListPage<ProbeThingResult>> ListAsync(
        ProbeListQuery query,
        CancellationToken cancellationToken
    ) => Query<ProbeThingRow>()
        .List(query)
        .SearchIn(row => row.Label)
        .Filter(
            values: query.Kind,
            column: row => row.Kind,
            facet: nameof(query.Kind)
        )
        .Filter(
            values: query.Active,
            column: row => row.Active,
            facet: nameof(query.Active)
        )
        .OrderBy(row => row.Label)
        .ToPageAsync(
            projection: row => new ProbeThingResult(
                row.Id,
                row.Label,
                row.Kind,
                row.Active
            ),
            cancellationToken: cancellationToken
        );
}
