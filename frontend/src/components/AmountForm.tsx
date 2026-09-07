import { type FormEvent, useState } from "react";
import type { ApiProblem } from "../api/client";
import { faultyFields } from "../api/problems";
import { Problem } from "./Problem";

// Un formulaire à un montant, pour le dépôt comme pour le retrait : la devise
// est celle du compte, le serveur refuse l'autre (CURRENCY_MISMATCH).
export function AmountForm({
  title,
  action,
  currency,
  onSubmit,
}: {
  title: string;
  action: string;
  currency: string;
  onSubmit: (amount: number) => Promise<ApiProblem | null>;
}) {
  const [amount, setAmount] = useState("");
  const [problem, setProblem] = useState<ApiProblem | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);

    const result = await onSubmit(Number(amount));

    setBusy(false);
    setProblem(result);

    if (!result) {
      setAmount("");
    }
  }

  const invalid = problem ? faultyFields(problem) : new Set<string>();

  return (
    <form className="card" onSubmit={submit}>
      <h3>{title}</h3>
      <label>
        Montant ({currency})
        <input
          type="number"
          step="0.01"
          min="0"
          value={amount}
          onChange={(event) => setAmount(event.target.value)}
          aria-invalid={invalid.has("amount") || undefined}
          required
        />
      </label>
      <button type="submit" disabled={busy}>
        {action}
      </button>
      <Problem problem={problem} />
    </form>
  );
}
