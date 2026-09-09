namespace LoreBank.SharedKernel.Application;

// La base d'une Liste (ADR 0027) : une query qui liste dérive d'elle, se lie
// [FromQuery] sur la query string, et rend une Page de son item — l'item
// reste le Result propre à la query (ADR 0012), l'enveloppe est du socle.
// Pagination par page numérotée, recherche textuelle libre ; les filtres
// sont les propriétés que la query dérivée déclare, typées et
// multi-valeurs (`currency=EUR&currency=USD` : OU dans un filtre, ET entre
// filtres) — ApplicationConventionTest épingle cette forme. Le tri n'est pas
// un paramètre : il est le sens de la lecture, fixé par le reader.
//
// Les bornes sont celles de toutes les Listes, non surchargeables : deux
// listes aux bornes différentes seraient deux formes que le front doit
// apprendre. Hors bornes, le moteur lève InvalidPagingException (422) quel
// que soit le chemin d'entrée — HTTP ou ISender.
public abstract record ListQuery<TItem> : IQuery<ListPage<TItem>>
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;

    public string? Search { get; init; }
}
