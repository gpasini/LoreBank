using System.Text.Json;
using LoreBank.SharedKernel.Contracts;

namespace LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

// La forme wire du payload d'outbox : camelCase, comme les paramètres des
// ProblemDetails — les clés partent telles quelles chez les consommateurs,
// la casse est un contrat, pas un détail de sérialiseur.
internal static class IntegrationEventJson
{
    private static readonly JsonSerializerOptions Options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    internal static string Serialize(IIntegrationEvent integrationEvent) =>
        JsonSerializer.Serialize(
            value: (object)integrationEvent,
            options: Options
        );

    internal static IIntegrationEvent Deserialize(
        string payload,
        Type eventType
    ) =>
        (IIntegrationEvent?)JsonSerializer.Deserialize(
            json: payload,
            returnType: eventType,
            options: Options
        )
        ?? throw new InvalidOperationException(
            $"Le payload d'outbox n'a pas pu être désérialisé en {eventType.Name}."
        );
}
