using LoreBank.SharedKernel.Application;

namespace LoreBank.Ledger.Application.Queries.ListLedgerMovements;

// La Liste des mouvements d'un compte (ADR 0027), sous la ressource : la
// route écrase AccountId (`query with { AccountId = id }` dans le
// controller), la query string porte page, recherche (sur l'identifiant
// d'écriture) et le filtre facetté sur le sens.
public sealed record ListLedgerMovementsQuery : ListQuery<LedgerMovementResult>
{
    [RouteBound]
    public Guid AccountId { get; init; }

    public IReadOnlyList<string>? Direction { get; init; }
}
