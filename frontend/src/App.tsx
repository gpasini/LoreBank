import { useCallback, useEffect, useState } from "react";
import { api, type ApiProblem } from "./api/client";
import type { components } from "./api/schema";
import { AccountDetail } from "./components/AccountDetail";
import { AccountList } from "./components/AccountList";
import { OpenAccountForm } from "./components/OpenAccountForm";
import { Problem } from "./components/Problem";
import { Facets, type FacetLabels } from "./listing/Facets";
import { Pager } from "./listing/Pager";
import { SearchBox } from "./listing/SearchBox";
import { useListing } from "./listing/useListing";
import { useSignals } from "./signals/SignalsProvider";

type Page = components["schemas"]["ListPageOfBankAccountSummaryResult"];

// La ressource que Bank signale : le genre stable de ses jumeaux publiés
// (MoneyDepositedIntegrationEvent.ResourceKind).
export const bankAccountKind = "bank-account";

// Les facettes de la Liste des comptes, nommées comme les filtres de la
// query (ListBankAccountsQuery) : le nom lie l'une à l'autre.
const facetLabels: FacetLabels = {
  currency: { title: "Devise" },
  isClosed: { title: "État", value: (value) => (value === "true" ? "Fermé" : "Ouvert") },
};

// L'écran : la Liste des comptes à gauche (avec l'ouverture), le détail du
// compte choisi à droite. Tout ce qui s'affiche vient du Client généré depuis
// la Description : aucun type de l'API n'est écrit ici. La Liste (ADR 0027)
// relit sur tout Signal de compte — un dépôt fait par un autre client, ou
// l'écriture du Ledger — en plus de ses propres commandes, avec les
// paramètres courants : page, recherche, filtres.
export function App() {
  const listing = useListing();
  const [page, setPage] = useState<Page | null>(null);
  const [problem, setProblem] = useState<ApiProblem | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const { page: pageNumber, search, filters } = listing;

  const reload = useCallback(async () => {
    const { data, error } = await api.GET("/api/bank/accounts", {
      params: {
        query: {
          page: pageNumber,
          search: search || undefined,
          currency: filters.currency,
          isClosed: filters.isClosed?.map((value) => value === "true"),
        },
      },
    });

    setPage(data ?? null);
    setProblem(error ?? null);
  }, [pageNumber, search, filters]);

  useEffect(() => {
    void reload();
  }, [reload]);

  useSignals(bankAccountKind, null, () => void reload());

  return (
    <main className="layout">
      <aside>
        <h1>LoreBank</h1>
        <OpenAccountForm
          onOpened={(id) => {
            setSelectedId(id);
            void reload();
          }}
        />
        <h2>Comptes</h2>
        <SearchBox value={search} onChange={listing.setSearch} placeholder="Rechercher un IBAN" />
        {page && <Facets facets={page.facets} selected={filters} labels={facetLabels} onToggle={listing.toggle} />}
        <Problem problem={problem} />
        <AccountList accounts={page?.items ?? []} selectedId={selectedId} onSelect={setSelectedId} />
        {page && <Pager page={page.page} pageSize={page.pageSize} totalCount={page.totalCount} onChange={listing.setPage} />}
      </aside>
      <section className="detail">
        {selectedId ? (
          <AccountDetail key={selectedId} accountId={selectedId} onChanged={() => void reload()} />
        ) : (
          <p className="muted">Choisissez un compte, ou ouvrez-en un.</p>
        )}
      </section>
    </main>
  );
}
