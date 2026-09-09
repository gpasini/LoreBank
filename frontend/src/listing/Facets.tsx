import type { components } from "../api/schema";

type Facet = components["schemas"]["Facet"];

// Comment une facette s'affiche : son titre, et la traduction de ses valeurs
// (une valeur arrive brute — `"true"`, `"EUR"` — c'est ici qu'on la nomme).
export type FacetLabels = Record<string, { title: string; value?: (value: string) => string }>;

// Les facettes d'une Liste (ADR 0027) : un groupe de cases à cocher par
// facette, chaque valeur avec le compte que cocher donnerait. Une valeur
// cochée que la recherche a fait disparaître reste affichée à 0, pour
// pouvoir la décocher.
export function Facets({
  facets,
  selected,
  labels,
  onToggle,
}: {
  facets: Facet[];
  selected: Record<string, string[]>;
  labels: FacetLabels;
  onToggle: (facet: string, value: string) => void;
}) {
  return (
    <div className="facets">
      {facets.map((facet) => {
        const checked = selected[facet.name] ?? [];
        const missing = checked.filter((value) => !facet.values.some((item) => item.value === value));
        const values = [...facet.values, ...missing.map((value) => ({ value, count: 0 }))];
        const label = labels[facet.name];

        if (values.length === 0) {
          return null;
        }

        return (
          <fieldset key={facet.name} className="facet">
            <legend>{label?.title ?? facet.name}</legend>
            {values.map((item) => (
              <label key={item.value} className="facet-value">
                <input
                  type="checkbox"
                  checked={checked.includes(item.value)}
                  onChange={() => onToggle(facet.name, item.value)}
                />
                <span>{label?.value ? label.value(item.value) : item.value}</span>
                <span className="count">{item.count}</span>
              </label>
            ))}
          </fieldset>
        );
      })}
    </div>
  );
}
