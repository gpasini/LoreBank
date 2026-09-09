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

// Les Instants arrivent en ISO 8601 UTC (ADR 0024) : c'est ici qu'on les
// affiche, dans le fuseau et la locale du navigateur.
export function instant(value: string): string {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(date);
}
