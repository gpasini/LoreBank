using LoreBank.SharedKernel.Contracts;

namespace LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

// La lecture unique du discriminant : l'attribut est obligatoire (un event
// sans discriminant partirait sous son nom de type .NET — précisément ce que
// le discriminant stable interdit), et son premier segment nomme le module
// publieur — c'est lui qui désigne l'outbox de destination.
internal static class IntegrationEventDiscriminant
{
    internal static string Of(Type eventType)
    {
        var attribute = (IntegrationEventAttribute?) Attribute.GetCustomAttribute(
            element: eventType,
            attributeType: typeof(IntegrationEventAttribute)
        );

        if (attribute is null) {
            throw new InvalidOperationException(
                $"L'integration event {eventType.Name} ne porte pas d'[IntegrationEvent(\"<module>.<fait>\")] : "
                + "sans discriminant stable choisi, l'outbox stockerait un nom de type .NET, et renommer un "
                + "namespace deviendrait une migration de données."
            );
        }

        return attribute.Discriminant;
    }

    internal static string ModuleOf(string discriminant)
    {
        var separator = discriminant.IndexOf('.');

        if (separator < 1) {
            throw new InvalidOperationException(
                $"Le discriminant « {discriminant} » ne suit pas la forme <module>.<fait> : le premier segment "
                + "nomme le module publieur en minuscules — c'est lui qui désigne le schéma de l'outbox."
            );
        }

        return discriminant[..separator];
    }
}
