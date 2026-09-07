using LoreBank.SharedKernel.Application;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// La forme d'une route mixte : la route écrase ThingId, le body ne porte que
// Amount — c'est ce que [RouteBound] fait dire à la Description.
public sealed record ProbeMixedCommand(
    [property: RouteBound] Guid ThingId,
    decimal Amount
) : ICommand;
