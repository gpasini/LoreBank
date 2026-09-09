using System.Diagnostics;

namespace LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

// La trace des livraisons d'outbox (ADR 0025) : une Activity par handler
// consommateur, posée en System.Diagnostics sans exporteur — le pendant des
// jauges d'OutboxMetrics, sur la source du même nom. Les jauges disent
// combien, l'activité dit quoi et combien de temps. Enfant de la trace de la
// commande d'origine quand la ligne a gardé son traceparent : le chemin
// publieur → consommateur se lit d'un bloc. Aucune activité par passe ni par
// purge — le fait d'intérêt est la livraison d'un event, pas le tick. Le nom
// est un nom stable comme le discriminant, jamais un type .NET ; seul
// l'attribut du handler en porte un.
public static class OutboxTracing
{
    public const string SourceName = "LoreBank.Outbox";

    public const string PublisherModuleTag = "lorebank.module";

    public const string ConsumerModuleTag = "lorebank.consumer.module";

    public const string HandlerTag = "lorebank.handler";

    public const string MessageIdTag = "messaging.message.id";

    public const string AttemptTag = "lorebank.attempt";

    private static readonly ActivitySource Source = new(SourceName);

    // Nulle sans listener (le contrat d'ActivitySource) : sans exporteur ni
    // sonde, rien n'est alloué et les appelants tolèrent l'absence.
    public static Activity? StartHandling(
        string publisherModule,
        string consumerModule,
        string handler,
        string discriminant,
        Guid messageId,
        int attempt,
        string? traceParent
    )
    {
        var parent = traceParent is not null
                     && ActivityContext.TryParse(
                         traceParent: traceParent,
                         traceState: null,
                         isRemote: true,
                         context: out var parsed
                     )
            ? parsed
            : default;

        KeyValuePair<string, object?>[] tags = [
                new KeyValuePair<string, object?>(
                    key: PublisherModuleTag,
                    value: publisherModule
                ),
                new KeyValuePair<string, object?>(
                    key: ConsumerModuleTag,
                    value: consumerModule
                ),
                new KeyValuePair<string, object?>(
                    key: HandlerTag,
                    value: handler
                ),
                new KeyValuePair<string, object?>(
                    key: MessageIdTag,
                    value: messageId.ToString()
                ),
                new KeyValuePair<string, object?>(
                    key: AttemptTag,
                    value: attempt
                ),
            ];

        // Positionnel à dessein : les deux surcharges de StartActivity ont les
        // mêmes noms de paramètres (name en tête, ou en queue via
        // CallerMemberName), et l'appel nommé est ambigu pour le compilateur.
        return Source.StartActivity(
            $"process {discriminant}",
            ActivityKind.Consumer,
            parent,
            tags
        );
    }
}
