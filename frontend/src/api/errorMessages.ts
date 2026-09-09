import type { ErrorCode } from "./client";
import { money } from "../format";

// La table de traduction des codes d'erreur (docs/erreurs.md, « Côté front »).
// Typée sur l'union ErrorCode du Client : un code que le back ajoute et que
// cette table ne traduit pas est une erreur de compilation, pas un code brut
// affiché à l'utilisateur. Les paramètres arrivent bruts (primitives) — c'est
// ici qu'on formate, avec la locale de l'utilisateur.
export const errorMessages: Record<ErrorCode, (parameters: Record<string, unknown>) => string> = {
  "BANK.ACCOUNT_CLOSED": () => "Ce compte est fermé.",
  "BANK.BANK_ACCOUNT_NOT_FOUND": () => "Ce compte est introuvable.",
  "BANK.INSUFFICIENT_BALANCE": ({ balance, requested, currency }) =>
    `Solde insuffisant : ${money(Number(requested), String(currency))} demandés pour un solde de ${money(Number(balance), String(currency))}.`,
  "BANK.NON_EMPTY_ACCOUNT_CLOSURE": () => "Un compte ne se ferme qu'à solde nul.",
  CONCURRENT_UPDATE: () => "Cet élément a été modifié entre-temps. Rechargez-le et recommencez.",
  CURRENCY_MISMATCH: ({ left, right }) => `Opération impossible entre ${String(left)} et ${String(right)}.`,
  INVALID_ACTOR: ({ actor }) => `« ${String(actor)} » n'est pas un identifiant d'acteur valide.`,
  INVALID_BIC: ({ bic }) => `« ${String(bic)} » n'est pas un BIC valide.`,
  INVALID_CURRENCY: ({ currency }) => `« ${String(currency)} » n'est pas une devise connue.`,
  INVALID_IBAN: ({ iban }) => `« ${String(iban)} » n'est pas un IBAN valide.`,
  INVALID_PAGING: ({ maxPageSize }) => `La page demandée n'existe pas (au plus ${String(maxPageSize)} éléments par page).`,
  INVALID_SIGNAL_RESOURCE: ({ resource }) => `« ${String(resource)} » n'est pas une ressource de Signal valide.`,
  "LEDGER.EMPTY_JOURNAL_ENTRY": () => "Une écriture comptable ne peut pas être vide.",
  "LEDGER.INVALID_LEDGER_ACCOUNT_REF": ({ value }) => `« ${String(value)} » n'est pas une référence de compte.`,
  "LEDGER.JOURNAL_ENTRY_NOT_FOUND": () => "Cette écriture comptable est introuvable.",
  "LEDGER.UNBALANCED_JOURNAL_ENTRY": () => "L'écriture comptable n'est pas équilibrée.",
  "LEDGER.UNKNOWN_BANK_ACCOUNT": () => "Aucune écriture comptable pour ce compte.",
  NON_POSITIVE_AMOUNT: () => "Le montant doit être strictement positif.",
  VALIDATION_FAILED: () => "La requête est mal formée.",
};
