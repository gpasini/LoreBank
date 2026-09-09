using LoreBank.SharedKernel.Application;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// La forme d'une Liste (ADR 0027) : la base porte page, pageSize et search ;
// la query dérivée déclare ses filtres, typés et multi-valeurs — une chaîne
// et un booléen, les deux natures que le moteur doit savoir facetter.
public sealed record ProbeListQuery : ListQuery<ProbeThingResult>
{
    public IReadOnlyList<string>? Kind { get; init; }

    public IReadOnlyList<bool>? Active { get; init; }
}
