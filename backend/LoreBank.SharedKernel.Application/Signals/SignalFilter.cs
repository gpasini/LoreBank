using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.SharedKernel.Application.Signals;

// Ce qu'un client veut recevoir : tout, ou les Signaux de quelques
// ressources — le paramètre ?resource=kind/guid, répétable, absent = tout.
// Le filtre est un choix du client, pas une garde : l'autorisation est
// l'affaire d'ISignalPolicy.
public sealed class SignalFilter
{
    private readonly HashSet<SignalResource> _resources;

    private SignalFilter(IEnumerable<SignalResource> resources)
    {
        _resources = resources.ToHashSet();
    }

    public static SignalFilter All { get; } = new([]);

    public bool IsAll => _resources.Count == 0;

    public static SignalFilter Of(IEnumerable<SignalResource> resources) => new(resources);

    public static SignalFilter Parse(IEnumerable<string> resources) => new(resources.Select(SignalResource.Parse));

    public bool Matches(Signal signal) =>
        IsAll
        || _resources.Contains(SignalResource.Of(
            kind: signal.ResourceKind,
            id: signal.ResourceId
        ));
}
