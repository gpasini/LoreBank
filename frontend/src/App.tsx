import { useEffect, useState } from "react";
import { api, type ApiProblem } from "./api/client";
import { errorMessages } from "./api/errorMessages";

// L'appel de démonstration : un GET typé de bout en bout. `data` est le
// BankAccountResult de la Description, `error` un ApiProblem — et `translate`
// ne connaît que des codes que le back peut vraiment servir.
type Account = NonNullable<Awaited<ReturnType<typeof loadAccount>>["data"]>;

function loadAccount(id: string) {
  return api.GET("/api/bank/accounts/{id}", { params: { path: { id } } });
}

function translate(problem: ApiProblem): string {
  // Le 500 est la seule réponse sans `code` : un message générique par statut.
  if (problem.code === undefined) {
    return `Erreur ${problem.status} — réessayez plus tard.`;
  }

  return errorMessages[problem.code](problem.parameters ?? {});
}

export function App() {
  const [id, setId] = useState("");
  const [account, setAccount] = useState<Account | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    if (id.length !== 36) {
      return;
    }

    let cancelled = false;

    loadAccount(id).then(({ data, error }) => {
      if (cancelled) {
        return;
      }

      setAccount(data ?? null);
      setMessage(error ? translate(error) : null);
    });

    return () => {
      cancelled = true;
    };
  }, [id]);

  return (
    <main>
      <h1>LoreBank</h1>
      <label>
        Identifiant du compte{" "}
        <input value={id} onChange={(event) => setId(event.target.value)} size={40} />
      </label>
      {account && (
        <p>
          {account.iban} — {account.balance} {account.currency}
          {account.isClosed ? " (fermé)" : ""}
        </p>
      )}
      {message && <p role="alert">{message}</p>}
    </main>
  );
}
