import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { Facets } from "./Facets";

const facets = [
  {
    name: "currency",
    values: [
      { value: "EUR", count: 3 },
      { value: "USD", count: 1 },
    ],
  },
];

describe("Facets", () => {
  it("affiche chaque valeur avec son compte, traduite par ses labels", () => {
    render(
      <Facets
        facets={facets}
        selected={{}}
        labels={{ currency: { title: "Devise", value: (v) => `en ${v}` } }}
        onToggle={vi.fn()}
      />,
    );

    expect(screen.getByText("Devise")).toBeDefined();
    expect(screen.getByText("en EUR")).toBeDefined();
    expect(screen.getByText("3")).toBeDefined();
  });

  it("garde une valeur cochée que la recherche a fait disparaître, à 0", () => {
    render(
      <Facets
        facets={facets}
        selected={{ currency: ["CHF"] }}
        labels={{}}
        onToggle={vi.fn()}
      />,
    );

    expect(screen.getByText("CHF")).toBeDefined();
    expect((screen.getByLabelText(/CHF/) as HTMLInputElement).checked).toBe(
      true,
    );
    expect(screen.getByText("0")).toBeDefined();
  });

  it("signale la facette et la valeur cochées", () => {
    const onToggle = vi.fn();

    render(
      <Facets facets={facets} selected={{}} labels={{}} onToggle={onToggle} />,
    );

    fireEvent.click(screen.getByLabelText(/USD/));

    expect(onToggle).toHaveBeenCalledWith("currency", "USD");
  });
});
