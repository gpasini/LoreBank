namespace LoreBank.SharedKernel.Contracts;

// L'opt-in d'un integration event vers les clients (ADR 0026) : l'event qui
// l'implémente nomme la ressource qu'il concerne, et le socle pousse un
// Signal — nu : discriminant, ressource, Instant — aux clients abonnés une
// fois la ligne d'outbox livrée à tous ses handlers. Un fait qui n'est pas
// publié ne peut pas être signalé : signaler commence par publier.
//
// ResourceKind est un nom stable choisi en kebab-case (« bank-account »),
// la même grille que le discriminant — jamais un nom de type .NET ; c'est
// lui, avec ResourceId, que le client passe en filtre (?resource=kind/id).
public interface ISignalsClients : IIntegrationEvent
{
    string ResourceKind { get; }

    Guid ResourceId { get; }
}
