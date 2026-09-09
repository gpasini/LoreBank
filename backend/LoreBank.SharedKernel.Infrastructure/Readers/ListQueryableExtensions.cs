using LoreBank.SharedKernel.Application;

namespace LoreBank.SharedKernel.Infrastructure.Readers;

// L'entrée du moteur de Liste (ADR 0027) : sur la row keyless que Query<TRow>
// sert, le reader déclare sa Liste — colonnes recherchées, filtres et
// facettes, tri — et le moteur exécute. Un reader n'écrit jamais son
// Skip/Take, son COUNT ni le GROUP BY d'une facette.
public static class ListQueryableExtensions
{
    public static ListBuilder<TRow, TItem> List<TRow, TItem>(
        this IQueryable<TRow> rows,
        ListQuery<TItem> query
    ) where TRow : class => new(
        source: rows,
        query: query
    );
}
