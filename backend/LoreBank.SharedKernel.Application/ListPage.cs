namespace LoreBank.SharedKernel.Application;

// La Page d'une Liste (ADR 0027) : l'enveloppe unique que toute ListQuery
// rend — pas un Result (un Result appartient à une query, ADR 0012), une
// forme du socle comme ApiProblem l'est pour les erreurs. Toujours complète :
// une page au-delà de la dernière est une Page vide avec son TotalCount,
// jamais un 404 ; une Liste sans facette porte une liste vide.
public sealed record ListPage<TItem>(
    IReadOnlyList<TItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<Facet> Facets
);
