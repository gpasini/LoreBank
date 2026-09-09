using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using LoreBank.SharedKernel.Application.Signals;
using LoreBank.SharedKernel.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LoreBank.SharedKernel.Infrastructure.Signals;

// Le fan-out des Signaux (ADR 0026) : un abonné par connexion, un canal
// borné par abonné, et chaque Signal poussé à ceux dont le filtre et la
// policy l'admettent. Un abonné qui ne lit pas assez vite déborde son canal :
// on ferme son abonnement plutôt que de perdre un Signal en silence — le
// client reconnecte et relit ce qu'il affiche. Singleton de l'hôte, sans
// état durable : ce qu'il ne pousse pas n'est pas perdu, la ligne d'outbox
// reste. Ses abonnés sont ceux de cette instance ; le suiveur de chaque
// instance lui apporte les livraisons de toutes.
public sealed class SignalHub(
    ISignalPolicy policy,
    SignalMetrics metrics,
    ILogger<SignalHub> logger
) : ISignalStream
{
    public const int SubscriberCapacity = 64;

    private readonly ConcurrentDictionary<Subscription, byte> _subscriptions = new();

    public int SubscriberCount => _subscriptions.Count;

    public async IAsyncEnumerable<Signal> SubscribeAsync(
        SignalFilter filter,
        Actor actor,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var subscription = new Subscription(
            Filter: filter,
            Actor: actor,
            Channel: Channel.CreateBounded<Signal>(new BoundedChannelOptions(SubscriberCapacity) {
                SingleReader = true,
                SingleWriter = false,
            }
            )
        );

        _subscriptions.TryAdd(
            key: subscription,
            value: 0
        );
        metrics.SubscriberJoined();

        try {
            await foreach (var signal in subscription.Channel.Reader.ReadAllAsync(cancellationToken)) {
                yield return signal;
            }
        } finally {
            _subscriptions.TryRemove(
                key: subscription,
                value: out _
            );
            metrics.SubscriberLeft();
        }
    }

    public void Publish(Signal signal)
    {
        foreach (var subscription in _subscriptions.Keys) {
            if (!subscription.Filter.Matches(signal) || !policy.CanReceive(
                    actor: subscription.Actor,
                    signal: signal
                )) {
                continue;
            }

            if (subscription.Channel.Writer.TryWrite(signal)) {
                metrics.Delivered();

                continue;
            }

            // Le canal est plein : l'abonné ne suit pas. Fermer plutôt que
            // jeter — la fin du flux est ce que le client sait rattraper.
            if (subscription.Channel.Writer.TryComplete()) {
                logger.LogWarning(
                    message: "Un abonné aux Signaux ({Actor}) a débordé son canal de {Capacity} : abonnement fermé, le client reconnectera.",
                    subscription.Actor,
                    SubscriberCapacity
                );
            }
        }
    }

    private sealed record Subscription(
        SignalFilter Filter,
        Actor Actor,
        Channel<Signal> Channel
    );
}
