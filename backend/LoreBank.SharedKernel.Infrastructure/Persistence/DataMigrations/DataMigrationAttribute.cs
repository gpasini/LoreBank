namespace LoreBank.SharedKernel.Infrastructure.Persistence.DataMigrations;

// Le jumeau de [Migration] d'EF : porte le timestamp (14 chiffres, la forme
// des ids EF) qui place la migration de données dans la timeline du module —
// un nom de classe C# ne peut pas commencer par un chiffre. L'id complet,
// « <timestamp>_<NomDeClasse> », est dérivé par DataMigrations.IdOf.
[AttributeUsage(AttributeTargets.Class)]
public sealed class DataMigrationAttribute(string timestamp) : Attribute
{
    public string Timestamp { get; } = timestamp;
}
