using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.SharedKernel.Application.Signals;

// Le port par lequel le bord HTTP s'abonne aux Signaux (ADR 0026) : l'Api
// ne connaît pas le hub de l'Infrastructure, elle consomme ce flux — même
// géométrie qu'ICurrentActor. L'énumération dure tant que le client est
// connecté ; elle se termine d'elle-même quand le socle ferme l'abonnement
// (client trop lent : canal borné débordé), et le client reconnecte.
public interface ISignalStream
{
    IAsyncEnumerable<Signal> SubscribeAsync(
        SignalFilter filter,
        Actor actor,
        CancellationToken cancellationToken
    );
}
