import { useCallback, useEffect, useState } from "react";
import { api, type ApiProblem } from "./api/client";
import type { components } from "./api/schema";
import { AccountDetail } from "./components/AccountDetail";
import { AccountList } from "./components/AccountList";
import { OpenAccountForm } from "./components/OpenAccountForm";
import { Problem } from "./components/Problem";
import { useSignals } from "./signals/SignalsProvider";

type Summary = components["schemas"]["BankAccountSummaryResult"];

// La ressource que Bank signale : le genre stable de ses jumeaux publiés
// (MoneyDepositedIntegrationEvent.ResourceKind).
export const bankAccountKind = "bank-account";

// L'écran : la liste des comptes à gauche (avec l'ouverture), le détail du
// compte choisi à droite. Tout ce qui s'affiche vient du Client généré depuis
// la Description : aucun type de l'API n'est écrit ici. La liste relit sur
// tout Signal de compte — un dépôt fait par un autre client, ou l'écriture
// du Ledger — en plus de ses propres commandes.
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
