namespace LoreBank.SharedKernel.Contracts;

// Le discriminant stocké dans la ligne d'outbox est un nom stable choisi
// (« bank.money-deposited »), jamais un nom de type .NET : renommer un
// namespace ne doit pas être une migration de données. Le premier segment
// nomme le module publieur en minuscules — la même grille que les codes
// d'erreur (BANK.X) — et c'est lui qui désigne le schéma où l'outbox vit.
[AttributeUsage(AttributeTargets.Class)]
public sealed class IntegrationEventAttribute(string discriminant) : Attribute
{
    public string Discriminant { get; } = discriminant;
}
