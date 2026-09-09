using LoreBank.SharedKernel.Contracts;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// La sonde des tests d'outbox : ancrée sur le ProbeModule (le discriminant
// désigne son schéma), comme les sondes de data migration s'ancrent sur
// ProbeDbContext — le socle se prouve sur son propre terrain, jamais sur un
// module d'exemple supprimable (ADR 0017). Elle signale les clients (ADR
// 0026) : c'est sur elle que le suiveur et le flux se prouvent — avec ses
// deux handlers, dont un qui échoue à la demande, pour épingler « après
// livraison ».
[IntegrationEvent("probe.probe-happened")]
public sealed record ProbeIntegrationEvent(
    Guid ThingId,
    string Label
) : ISignalsClients
{
    public const string ThingResourceKind = "probe-thing";

    public string ResourceKind => ThingResourceKind;

    public Guid ResourceId => ThingId;
}
