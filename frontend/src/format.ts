// Les montants arrivent bruts (docs/erreurs.md) : c'est ici qu'on formate,
// avec la locale du navigateur.
export function money(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(amount);
  } catch {
    return `${amount} ${currency}`;
  }
}

export function iban(value: string): string {
  return value.replace(/(.{4})/g, "$1 ").trim();
}
