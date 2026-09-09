using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.SharedKernel.Domain.ValueObjects;

// L'Acteur : qui agit — humain ou système — identifié par l'identifiant
// opaque que le fournisseur d'identité lui donne, et rien d'autre (ADR 0023).
// Deux états : identifié, ou Anonyme tant que personne n'authentifie. Le
// Domain ne demande jamais « qui agit » : il reçoit l'Acteur en paramètre de
// transition, fourni aux handlers par le port ICurrentActor de l'Application.
public sealed class Actor : ValueObject
{
    private readonly string? _id;

    private Actor(string? id)
    {
        _id = id;
    }

    public static Actor Anonymous { get; } = new(null);

    public bool IsAnonymous => _id is null;

    // Lisible sur un Acteur identifié seulement : un Anonyme n'a pas
    // d'identifiant, et le lire serait enregistrer un fait sous une fausse
    // identité — l'appelant teste IsAnonymous.
    public string Id => _id ?? throw new InvalidOperationException("Un Acteur anonyme n'a pas d'identifiant.");

    // Création : l'entrée vient d'une frontière — normaliser d'abord, valider
    // ensuite. Un identifiant vide n'est pas un Anonyme, c'est une erreur.
    public static Actor Of(string id)
    {
        var normalized = id.Trim();

        if (normalized.Length == 0) {
            throw new InvalidActorException(id);
        }

        return new Actor(normalized);
    }

    // Réhydratation : null est l'Anonyme — la forme de l'absence hors du
    // Domain — et toute autre valeur est reprise telle quelle (ADR 0016).
    // Jamais appelé depuis du code métier.
    public static Actor Hydrate(string? id) => id is null ? Anonymous : new Actor(id);

    protected override IEnumerable<object?> GetEqualityComponents() => [_id];

    public override string ToString() => _id ?? "anonymous";
}
