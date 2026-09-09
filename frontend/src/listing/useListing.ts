import { useCallback, useState } from "react";

// L'état d'une Liste (ADR 0027) côté écran : la page, la recherche et les
// filtres cochés — par nom de facette, valeurs en chaînes telles que la
// Page les sert. Changer la recherche ou un filtre ramène en page 1 ; la
// page seule bouge par le pager. L'état vit ici, pas dans l'URL : la relire
// après un Signal rejoue ces mêmes paramètres.
export type ListingState = {
  page: number;
  search: string;
  filters: Record<string, string[]>;
};

export function useListing(initial?: Partial<ListingState>) {
  const [state, setState] = useState<ListingState>({
    page: 1,
    search: "",
    filters: {},
    ...initial,
  });

  const setSearch = useCallback(
    (search: string) =>
      setState((current) => ({ ...current, search, page: 1 })),
    [],
  );

  const setPage = useCallback(
    (page: number) => setState((current) => ({ ...current, page })),
    [],
  );

  const toggle = useCallback(
    (facet: string, value: string) =>
      setState((current) => {
        const selected = current.filters[facet] ?? [];
        const next = selected.includes(value)
          ? selected.filter((item) => item !== value)
          : [...selected, value];

        return {
          ...current,
          page: 1,
          filters: { ...current.filters, [facet]: next },
        };
      }),
    [],
  );

  return { ...state, setSearch, setPage, toggle };
}
