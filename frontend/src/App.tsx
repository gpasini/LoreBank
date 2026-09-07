import { useCallback, useEffect, useState } from "react";
import { api, type ApiProblem } from "./api/client";
import type { components } from "./api/schema";
import { AccountDetail } from "./components/AccountDetail";
import { AccountList } from "./components/AccountList";
import { OpenAccountForm } from "./components/OpenAccountForm";
import { Problem } from "./components/Problem";

type Summary = components["schemas"]["BankAccountSummaryResult"];

// L'écran : la liste des comptes à gauche (avec l'ouverture), le détail du
// compte choisi à droite. Tout ce qui s'affiche vient du Client généré depuis
// la Description : aucun type de l'API n'est écrit ici.
export function App() {
  const [accounts, setAccounts] = useState<Summary[]>([]);
  const [problem, setProblem] = useState<ApiProblem | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const reload = useCallback(async () => {
    const { data, error } = await api.GET("/api/bank/accounts");

    setAccounts(data?.accounts ?? []);
    setProblem(error ?? null);
  }, []);

  useEffect(() => {
    void reload();
  }, [reload]);

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
        <Problem problem={problem} />
        <AccountList accounts={accounts} selectedId={selectedId} onSelect={setSelectedId} />
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
