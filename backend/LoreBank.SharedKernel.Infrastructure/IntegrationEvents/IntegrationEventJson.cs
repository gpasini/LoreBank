using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using LoreBank.SharedKernel.Contracts;

namespace LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

// La forme wire du payload d'outbox : camelCase, comme les paramètres des
// ProblemDetails — les clés partent telles quelles chez les consommateurs,
// la casse est un contrat, pas un détail de sérialiseur. La ressource qu'un
// event nomme pour les clients (ISignalsClients, ADR 0026) n'en fait pas
// partie : elle vit dans ses propres colonnes, et le payload reste le
// langage publié de l'event — rien de plus.
internal static class IntegrationEventJson
{
    private readonly static JsonSerializerOptions Options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver {
            Modifiers = { WithoutSignalResource },
        },
    };

    internal static string Serialize(IIntegrationEvent integrationEvent) =>
        JsonSerializer.Serialize(
            value: (object) integrationEvent,
            options: Options
        );

    internal static IIntegrationEvent Deserialize(
        string payload,
        Type eventType
    ) =>
        (IIntegrationEvent?) JsonSerializer.Deserialize(
            json: payload,
            returnType: eventType,
            options: Options
        )
        ?? throw new InvalidOperationException(
            $"Le payload d'outbox n'a pas pu être désérialisé en {eventType.Name}."
        );

    private static void WithoutSignalResource(JsonTypeInfo typeInfo)
    {
        if (!typeof(ISignalsClients).IsAssignableFrom(typeInfo.Type)) {
            return;
        }

        // Le nom est déjà passé par la politique de nommage (camelCase) :
        // la comparaison ignore la casse.
        foreach (var property in typeInfo.Properties.ToList()) {
            if (string.Equals(
                    a: property.Name,
                    b: nameof(ISignalsClients.ResourceKind),
                    comparisonType: StringComparison.OrdinalIgnoreCase
                ) || string.Equals(
                    a: property.Name,
                    b: nameof(ISignalsClients.ResourceId),
                    comparisonType: StringComparison.OrdinalIgnoreCase
                )) {
                typeInfo.Properties.Remove(property);
            }
        }
    }
}
