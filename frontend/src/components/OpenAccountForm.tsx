import { type FormEvent, useState } from "react";
import { api, type ApiProblem } from "../api/client";
import { faultyFields } from "../api/problems";
import { Problem } from "./Problem";

const currencies = ["EUR", "USD", "GBP", "CHF"];

// Une création rend 201 + Location sans corps : l'identifiant créé se lit
// dans l'en-tête, et c'est le parent qui recharge la liste (CQS).
export function OpenAccountForm({ onOpened }: { onOpened: (id: string) => void }) {
  const [iban, setIban] = useState("");
  const [currency, setCurrency] = useState("EUR");
  const [problem, setProblem] = useState<ApiProblem | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setProblem(null);

    const { error, response } = await api.POST("/api/bank/accounts", {
      body: { iban: iban.replace(/\s+/g, ""), currency },
    });

    setBusy(false);

    if (error) {
      setProblem(error);
      return;
    }

    const location = response.headers.get("location") ?? "";
    const id = location.split("/").pop() ?? "";

    setIban("");
    onOpened(id);
  }

  const invalid = problem ? faultyFields(problem) : new Set<string>();

  return (
    <form className="card" onSubmit={submit}>
      <h2>Ouvrir un compte</h2>
      <label>
        IBAN
        <input
          value={iban}
          onChange={(event) => setIban(event.target.value)}
          placeholder="FR76 3000 6000 0112 3456 7890 189"
          aria-invalid={invalid.has("iban") || problem?.code === "INVALID_IBAN" || undefined}
          required
        />
      </label>
      <label>
        Devise
        <select value={currency} onChange={(event) => setCurrency(event.target.value)}>
          {currencies.map((code) => (
            <option key={code} value={code}>
              {code}
            </option>
          ))}
        </select>
      </label>
      <button type="submit" disabled={busy}>
        Ouvrir
      </button>
      <Problem problem={problem} />
    </form>
  );
}
