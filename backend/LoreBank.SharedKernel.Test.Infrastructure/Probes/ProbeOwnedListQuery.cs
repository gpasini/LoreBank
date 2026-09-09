using LoreBank.SharedKernel.Application;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// La forme d'une Liste sous une ressource (ADR 0027) : la route écrase
// OwnerId, la query string ne porte que page, pageSize, search et les
// filtres — c'est ce que [RouteBound] fait dire à la Description.
public sealed record ProbeOwnedListQuery : ListQuery<ProbeThingResult>
{
    [RouteBound]
    public Guid OwnerId { get; init; }

    public IReadOnlyList<string>? Kind { get; init; }
}
