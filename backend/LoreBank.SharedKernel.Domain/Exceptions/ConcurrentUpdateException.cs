namespace LoreBank.SharedKernel.Domain.Exceptions;

// Levée par le socle quand une écriture porte sur une Version d'agrégat
// périmée : une autre commande a écrit entre le chargement et la sauvegarde
// (ADR 0020). Le filtre d'exceptions en fait un 409. Le paramètre est la clé
// en primitive — la valeur provider, jamais le VO ni le nom du type .NET.
public sealed class ConcurrentUpdateException(object id) : DomainException(
    new() { ["id"] = id }
);
