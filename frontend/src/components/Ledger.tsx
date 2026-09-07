import { useEffect, useState } from "react";
import { api, type ApiProblem } from "../api/client";
import type { components } from "../api/schema";
import { money } from "../format";
import { Problem } from "./Problem";

type LedgerResult = components["schemas"]["BankAccountLedgerResult"];

// Les mouvements viennent du module Ledger, alimenté par les integration
// events de Bank : ils arrivent un instant après la commande (l'outbox est
// dépilée toutes les secondes). À chaque commande du parent (`version`), on
// relit tout de suite puis une seconde fois un peu plus tard ; le bouton
// couvre le reste.
export function Ledger({ accountId, version }: { accountId: string; version: number }) {
  const [ledger, setLedger] = useState<LedgerResult | null>(null);
  const [problem, setProblem] = useState<ApiProblem | null>(null);
  const [tick, setTick] = useState(0);

  useEffect(() => {
    let cancelled = false;

    function load() {
      api.GET("/api/ledger/bank-accounts/{id}", { params: { path: { id: accountId } } }).then(({ data, error }) => {
        if (cancelled) {
          return;
        }

        setLedger(data ?? null);
        setProblem(error ?? null);
      });
    }

    load();

    const later = window.setTimeout(load, 1500);

    return () => {
      cancelled = true;
      window.clearTimeout(later);
    };
  }, [accountId, version, tick]);

  return (
    <section className="card">
      <div className="row">
        <h3>Mouvements (Ledger)</h3>
        <button type="button" className="ghost" onClick={() => setTick((value) => value + 1)}>
          Recharger
        </button>
      </div>
      <Problem problem={problem} />
      {ledger && ledger.movements.length === 0 && <p className="muted">Aucun mouvement.</p>}
      {ledger && ledger.movements.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>Écriture</th>
              <th>Sens</th>
              <th className="num">Montant</th>
            </tr>
          </thead>
          <tbody>
            {ledger.movements.map((movement, index) => (
              <tr key={`${movement.entryId}-${index}`}>
                <td className="mono">{movement.entryId.slice(0, 8)}</td>
                <td>{movement.direction}</td>
                <td className="num">{money(movement.amount, movement.currency)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
