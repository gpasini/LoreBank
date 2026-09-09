namespace LoreBank.SharedKernel.Application.Signals;

// Le Signal (ADR 0026) : ce que le socle pousse aux clients quand un
// integration event a été livré. Nu — il dit quoi (le discriminant) et où
// (la ressource, en primitives comme un Result), jamais comment : le client
// refait son GET. OccurredAt est l'Instant du fait, pas celui de la
// livraison. C'est aussi la forme wire : le schéma que la Description
// publie sous le flux text/event-stream.
public sealed record Signal(
    string Discriminant,
    string ResourceKind,
    Guid ResourceId,
    DateTimeOffset OccurredAt
);
