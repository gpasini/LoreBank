namespace LoreBank.Probe.Infrastructure.Persistence;

// La table-terrain des sondes de data migration : une ligne dont la présence
// ou l'absence prouve le tout-ou-rien du runner. Volontairement pas un
// agrégat — le terrain ne porte aucun invariant métier, et les conventions du
// Domain (DomainConventionTest) ne s'appliquent qu'aux modules de
// HostModules.All. Kind et Active sont les colonnes de la sonde de Liste
// (ListContractTest, ADR 0027) : une chaîne et un booléen à facetter.
public sealed class ProbeThing
{
    public Guid Id { get; set; }

    public string Label { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public bool Active { get; set; }
}
