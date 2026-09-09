import { useEffect, useState } from "react";
import { api, type ApiProblem } from "../api/client";
import type { components } from "../api/schema";
import { instant, money } from "../format";
import { Problem } from "./Problem";

type LedgerResult = components["schemas"]["BankAccountLedgerResult"];

// Les mouvements viennent du module Ledger, alimenté par les integration
// events de Bank : ils arrivent un instant après la commande, par l'outbox.
// Le parent relit (`version`) après chaque commande et à chaque Signal du
// compte — le Signal part une fois la ligne du Ledger écrite, c'est lui qui
// amène le mouvement à l'écran.
export function Ledger({ accountId, version }: { accountId: string; version: number }) {
  const [ledger, setLedger] = useState<LedgerResult | null>(null);
  const [problem, setProblem] = useState<ApiProblem | null>(null);

  useEffect(() => {
    let cancelled = false;

    api.GET("/api/ledger/bank-accounts/{id}", { params: { path: { id: accountId } } }).then(({ data, error }) => {
      if (cancelled) {
        return;
      }

      setLedger(data ?? null);
      setProblem(error ?? null);
    });

    return () => {
      cancelled = true;
    };
  }, [accountId, version]);

  return (
    <section className="card">
      <h3>Mouvements (Ledger)</h3>
      <Problem problem={problem} />
      {ledger && ledger.movements.length === 0 && <p className="muted">Aucun mouvement.</p>}
      {ledger && ledger.movements.length > 0 && (
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
            {ledger.movements.map((movement, index) => (
              <tr key={`${movement.entryId}-${index}`}>
                <td>{instant(movement.recordedAt)}</td>
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
