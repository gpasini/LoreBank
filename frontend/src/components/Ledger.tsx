import { useCallback, useEffect, useState } from "react";
import { type ApiProblem, api } from "../api/client";
import type { components } from "../api/schema";
import { instant, money } from "../format";
import { type FacetLabels, Facets } from "../listing/Facets";
import { Pager } from "../listing/Pager";
import { SearchBox } from "../listing/SearchBox";
import { useListing } from "../listing/useListing";
import { Problem } from "./Problem";

type Page = components["schemas"]["ListPageOfLedgerMovementResult"];

// La facette de la Liste des mouvements, nommée comme le filtre de la query
// (ListLedgerMovementsQuery.Direction) ; ses valeurs sont celles du Ledger,
// traduites ici.
const direction = (value: string) => (value === "Debit" ? "Débit" : "Crédit");

const facetLabels: FacetLabels = {
  direction: { title: "Sens", value: direction },
};

// Les mouvements viennent du module Ledger, alimenté par les integration
// events de Bank : ils arrivent un instant après la commande, par l'outbox.
// Le parent relit (`version`) après chaque commande et à chaque Signal du
// compte — le Signal part une fois la ligne du Ledger écrite, c'est lui qui
// amène le mouvement à l'écran, en tête de la page 1 (du plus récent au
// plus ancien). Une Liste sous la ressource (ADR 0027) : recherche sur
// l'identifiant d'écriture, facette sur le sens, pager.
export function Ledger({
  accountId,
  version,
}: {
  accountId: string;
  version: number;
}) {
  const listing = useListing();
  const [page, setPage] = useState<Page | null>(null);
  const [problem, setProblem] = useState<ApiProblem | null>(null);

  const { page: pageNumber, search, filters } = listing;

  const reload = useCallback(async () => {
    const { data, error } = await api.GET(
      "/api/ledger/bank-accounts/{id}/movements",
      {
        params: {
          path: { id: accountId },
          query: {
            page: pageNumber,
            search: search || undefined,
            direction: filters.direction,
          },
        },
      },
    );

    setPage(data ?? null);
    setProblem(error ?? null);
  }, [accountId, pageNumber, search, filters]);

  // version est le compteur de Signaux (ADR 0026) : il n'est lu nulle part,
  // sa seule fonction est de relancer la lecture.
  // biome-ignore lint/correctness/useExhaustiveDependencies: version relance le GET sur Signal
  useEffect(() => {
    void reload();
  }, [reload, version]);

  return (
    <section className="card">
      <h3>Mouvements (Ledger)</h3>
      <Problem problem={problem} />
      {page && (page.totalCount > 0 || search || filters.direction?.length) ? (
        <>
          <SearchBox
            value={search}
            onChange={listing.setSearch}
            placeholder="Rechercher une écriture"
          />
          <Facets
            facets={page.facets}
            selected={filters}
            labels={facetLabels}
            onToggle={listing.toggle}
          />
        </>
      ) : null}
      {page && page.items.length === 0 && (
        <p className="muted">Aucun mouvement.</p>
      )}
      {page && page.items.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>Comptabilisé le</th>
              <th>Écriture</th>
              <th>Sens</th>
              <th className="num">Montant</th>
            </tr>
          </thead>
          <tbody>
            {page.items.map((movement) => (
              <tr key={`${movement.entryId}-${movement.direction}`}>
                <td>{instant(movement.recordedAt)}</td>
                <td className="mono">{movement.entryId.slice(0, 8)}</td>
                <td>{direction(movement.direction)}</td>
                <td className="num">
                  {money(movement.amount, movement.currency)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {page && (
        <Pager
          page={page.page}
          pageSize={page.pageSize}
          totalCount={page.totalCount}
          onChange={listing.setPage}
        />
      )}
    </section>
  );
}
