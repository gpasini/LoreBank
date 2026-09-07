import { useEffect, useState } from "react";
import { api, type ApiProblem } from "../api/client";
import type { components } from "../api/schema";
import { iban, money } from "../format";
import { AmountForm } from "./AmountForm";
import { Ledger } from "./Ledger";
import { Problem } from "./Problem";

type Account = components["schemas"]["BankAccountResult"];

// Le détail d'un compte et ses trois transitions. Une commande ne renvoie
// rien : après chacune, on relit le compte (GET) et on prévient le parent pour
// que la liste se recharge aussi — le CQS jusqu'au bord de l'écran.
export function AccountDetail({ accountId, onChanged }: { accountId: string; onChanged: () => void }) {
  const [account, setAccount] = useState<Account | null>(null);
  const [problem, setProblem] = useState<ApiProblem | null>(null);
  const [closing, setClosing] = useState<ApiProblem | null>(null);
  const [version, setVersion] = useState(0);

  useEffect(() => {
    let cancelled = false;

    api.GET("/api/bank/accounts/{id}", { params: { path: { id: accountId } } }).then(({ data, error }) => {
      if (cancelled) {
        return;
      }

      setAccount(data ?? null);
      setProblem(error ?? null);
    });

    return () => {
      cancelled = true;
    };
  }, [accountId, version]);

  function changed() {
    setVersion((value) => value + 1);
    onChanged();
  }

  async function deposit(amount: number): Promise<ApiProblem | null> {
    const { error } = await api.POST("/api/bank/accounts/{id}/deposits", {
      params: { path: { id: accountId } },
      body: { amount, currency: account?.currency ?? "" },
    });

    if (!error) {
      changed();
    }

    return error ?? null;
  }

  async function withdraw(amount: number): Promise<ApiProblem | null> {
    const { error } = await api.POST("/api/bank/accounts/{id}/withdrawals", {
      params: { path: { id: accountId } },
      body: { amount, currency: account?.currency ?? "" },
    });

    if (!error) {
      changed();
    }

    return error ?? null;
  }

  async function close() {
    setClosing(null);

    const { error } = await api.POST("/api/bank/accounts/{id}/closure", {
      params: { path: { id: accountId } },
    });

    if (error) {
      setClosing(error);
      return;
    }

    changed();
  }

  if (problem) {
    return <Problem problem={problem} />;
  }

  if (!account) {
    return <p className="muted">Chargement…</p>;
  }

  return (
    <>
      <header className="card account-header">
        <div>
          <p className="iban">{iban(account.iban)}</p>
          <p className="mono muted">{account.id}</p>
        </div>
        <div className="right">
          <p className="balance-big">{money(account.balance, account.currency)}</p>
          {account.isClosed ? (
            <span className="badge">Fermé</span>
          ) : (
            <button type="button" className="danger" onClick={close}>
              Clôturer
            </button>
          )}
          <Problem problem={closing} />
        </div>
      </header>

      {!account.isClosed && (
        <div className="grid">
          <AmountForm title="Dépôt" action="Déposer" currency={account.currency} onSubmit={deposit} />
          <AmountForm title="Retrait" action="Retirer" currency={account.currency} onSubmit={withdraw} />
        </div>
      )}

      <Ledger accountId={accountId} version={version} />
    </>
  );
}
