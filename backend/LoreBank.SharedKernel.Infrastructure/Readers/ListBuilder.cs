using System.Linq.Expressions;
using System.Text.Json;
using LoreBank.SharedKernel.Application;
using LoreBank.SharedKernel.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.SharedKernel.Infrastructure.Readers;

// Le moteur de Liste du socle (ADR 0027), déclaratif : le reader dit dans
// quelles colonnes chercher (SearchIn, OU entre colonnes), quels filtres de
// sa query s'appliquent à quelle colonne (Filter — OU dans un filtre, ET entre
// filtres — et, du même geste, la facette qu'il alimente), et le tri
// (OrderBy, obligatoire : un Skip/Take sans ordre rend des pages
// incohérentes). ToPageAsync exécute alors, dans l'ordre : le COUNT total,
// un GROUP BY par facette — chacune comptée sur la recherche et les *autres*
// filtres, jamais le sien : le compte dit ce que cocher donnerait —, puis la
// page projetée, où EF ne lit que les colonnes du Result.
//
// La recherche est une sous-chaîne insensible à la casse (ILIKE, jokers
// échappés — un `%` cherché est un `%` littéral), fidèle au provider du socle
// (ADR 0008) : le moteur se prouve sur PostgreSQL, ListContractTest.
public sealed class ListBuilder<TRow, TItem> where TRow : class
{
    private const string EscapeCharacter = @"\";

    private readonly IQueryable<TRow> _source;

    private readonly ListQuery<TItem> _query;

    private readonly List<Expression<Func<TRow, string>>> _searchColumns = [];

    private readonly List<Criterion> _criteria = [];

    private Func<IQueryable<TRow>, IOrderedQueryable<TRow>>? _order;

    internal ListBuilder(
        IQueryable<TRow> source,
        ListQuery<TItem> query
    )
    {
        _source = source;
        _query = query;
    }

    public ListBuilder<TRow, TItem> SearchIn(Expression<Func<TRow, string>> column)
    {
        _searchColumns.Add(column);

        return this;
    }

    // `values` est le filtre tel que la query le porte (null ou vide : pas de
    // filtre) ; `facet` est le nom de cette propriété — `nameof`, pour que le
    // renommage suive — que le moteur écrit en camelCase comme la clé JSON.
    public ListBuilder<TRow, TItem> Filter<TValue>(
        IReadOnlyList<TValue>? values,
        Expression<Func<TRow, TValue>> column,
        string? facet = null
    )
    {
        var selected = values?.ToList() ?? [];

        _criteria.Add(new Criterion(
                Predicate: selected.Count == 0
                    ? null
                    : Compose(
                        column: column,
                        match: value => selected.Contains(value)
                    ),
                FacetOf: facet is null
                    ? null
                    : (rows, cancellationToken) => CountAsync(
                        name: JsonNamingPolicy.CamelCase.ConvertName(facet),
                        column: column,
                        rows: rows,
                        cancellationToken: cancellationToken
                    )
            )
        );

        return this;
    }

    public ListBuilder<TRow, TItem> OrderBy<TKey>(Expression<Func<TRow, TKey>> key)
    {
        _order = rows => rows.OrderBy(key);

        return this;
    }

    public ListBuilder<TRow, TItem> OrderByDescending<TKey>(Expression<Func<TRow, TKey>> key)
    {
        _order = rows => rows.OrderByDescending(key);

        return this;
    }

    public ListBuilder<TRow, TItem> ThenBy<TKey>(Expression<Func<TRow, TKey>> key)
    {
        var previous = RequireOrder();
        _order = rows => previous(rows).ThenBy(key);

        return this;
    }

    public ListBuilder<TRow, TItem> ThenByDescending<TKey>(Expression<Func<TRow, TKey>> key)
    {
        var previous = RequireOrder();
        _order = rows => previous(rows).ThenByDescending(key);

        return this;
    }

    public async Task<ListPage<TItem>> ToPageAsync(
        Expression<Func<TRow, TItem>> projection,
        CancellationToken cancellationToken
    )
    {
        var order = RequireOrder();
        var page = _query.Page;
        var pageSize = _query.PageSize;

        if (page < 1 || pageSize < 1 || pageSize > ListQuery<TItem>.MaxPageSize) {
            throw new InvalidPagingException(
                page: page,
                pageSize: pageSize,
                maxPageSize: ListQuery<TItem>.MaxPageSize
            );
        }

        var searched = Searched(_source);
        var filtered = Filtered(
            rows: searched,
            except: null
        );

        var totalCount = await filtered.CountAsync(cancellationToken);

        var facets = new List<Facet>();

        foreach (var criterion in _criteria.Where(criterion => criterion.FacetOf is not null)) {
            facets.Add(await criterion.FacetOf!(
                    Filtered(
                        rows: searched,
                        except: criterion
                    ),
                    cancellationToken
                )
            );
        }

        var items = await order(filtered)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(projection)
            .ToListAsync(cancellationToken);

        return new ListPage<TItem>(
            Items: items,
            Page: page,
            PageSize: pageSize,
            TotalCount: totalCount,
            Facets: facets
        );
    }

    private Func<IQueryable<TRow>, IOrderedQueryable<TRow>> RequireOrder() => _order
        ?? throw new InvalidOperationException(
            "Une Liste est triée : OrderBy avant ThenBy ou ToPageAsync — un Skip/Take sans ordre rend des pages incohérentes."
        );

    private IQueryable<TRow> Searched(IQueryable<TRow> rows)
    {
        if (string.IsNullOrWhiteSpace(_query.Search) || _searchColumns.Count == 0) {
            return rows;
        }

        var pattern = $"%{Escape(_query.Search.Trim())}%";

        Expression<Func<string, bool>> matches = value => EF.Functions.ILike(
            value,
            pattern,
            EscapeCharacter
        );

        var row = Expression.Parameter(
            type: typeof(TRow),
            name: "row"
        );

        var body = _searchColumns
            .Select(column => ParameterReplacer.Replace(
                    expression: matches.Body,
                    parameter: matches.Parameters[0],
                    replacement: ParameterReplacer.Replace(
                        expression: column.Body,
                        parameter: column.Parameters[0],
                        replacement: row
                    )
                )
            )
            .Aggregate(Expression.OrElse);

        return rows.Where(Expression.Lambda<Func<TRow, bool>>(
                body: body,
                parameters: row
            )
        );
    }

    private IQueryable<TRow> Filtered(
        IQueryable<TRow> rows,
        Criterion? except
    ) => _criteria
        .Where(criterion => criterion != except && criterion.Predicate is not null)
        .Aggregate(
            seed: rows,
            func: (current, criterion) => current.Where(criterion.Predicate!)
        );

    private static async Task<Facet> CountAsync<TValue>(
        string name,
        Expression<Func<TRow, TValue>> column,
        IQueryable<TRow> rows,
        CancellationToken cancellationToken
    )
    {
        var groups = await rows
            .GroupBy(column)
            .Select(group => new {
                    group.Key,
                    Count = group.Count(),
                }
            )
            .ToListAsync(cancellationToken);

        var values = groups
            .Where(group => group.Key is not null)
            .Select(group => new FacetValue(
                    Value: FacetValue.Format(group.Key!),
                    Count: group.Count
                )
            )
            .OrderByDescending(value => value.Count)
            .ThenBy(
                keySelector: value => value.Value,
                comparer: StringComparer.Ordinal
            )
            .ToList();

        return new Facet(
            Name: name,
            Values: values
        );
    }

    private static Expression<Func<TRow, bool>> Compose<TValue>(
        Expression<Func<TRow, TValue>> column,
        Expression<Func<TValue, bool>> match
    ) => Expression.Lambda<Func<TRow, bool>>(
        body: ParameterReplacer.Replace(
            expression: match.Body,
            parameter: match.Parameters[0],
            replacement: column.Body
        ),
        parameters: column.Parameters
    );

    private static string Escape(string search) => search
        .Replace(
            oldValue: EscapeCharacter,
            newValue: EscapeCharacter + EscapeCharacter
        )
        .Replace(
            oldValue: "%",
            newValue: EscapeCharacter + "%"
        )
        .Replace(
            oldValue: "_",
            newValue: EscapeCharacter + "_"
        );

    // Un filtre déclaré : son prédicat (null quand la query ne le pose pas) et,
    // s'il alimente une facette, le compte de celle-ci sur les rows données.
    private sealed record Criterion(
        Expression<Func<TRow, bool>>? Predicate,
        Func<IQueryable<TRow>, CancellationToken, Task<Facet>>? FacetOf
    );

    private sealed class ParameterReplacer(
        ParameterExpression parameter,
        Expression replacement
    ) : ExpressionVisitor
    {
        public static Expression Replace(
            Expression expression,
            ParameterExpression parameter,
            Expression replacement
        ) => new ParameterReplacer(
            parameter: parameter,
            replacement: replacement
        ).Visit(expression);

        protected override Expression VisitParameter(ParameterExpression node) => node == parameter
            ? replacement
            : base.VisitParameter(node);
    }
}
