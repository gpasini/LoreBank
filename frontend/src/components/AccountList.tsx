import type { components } from "../api/schema";
import { iban, instant, money } from "../format";

type Summary = components["schemas"]["BankAccountSummaryResult"];

export function AccountList({
  accounts,
  selectedId,
  onSelect,
}: {
  accounts: Summary[];
  selectedId: string | null;
  onSelect: (id: string) => void;
}) {
  if (accounts.length === 0) {
    return <p className="muted">Aucun compte pour l'instant.</p>;
  }

  return (
    <ul className="accounts">
      {accounts.map((account) => (
        <li key={account.id}>
          <button
            type="button"
            className={account.id === selectedId ? "account selected" : "account"}
            onClick={() => onSelect(account.id)}
          >
            <span className="iban">{iban(account.iban)}</span>
            <span className="balance">{money(account.balance, account.currency)}</span>
            <span className="muted">Ouvert le {instant(account.openedAt)}</span>
            {account.isClosed && <span className="badge">Fermé</span>}
          </button>
        </li>
      ))}
    </ul>
  );
}
