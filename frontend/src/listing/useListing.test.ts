import { act, renderHook } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { useListing } from "./useListing";

describe("useListing", () => {
  it("part en page 1, sans recherche ni filtre", () => {
    const { result } = renderHook(() => useListing());

    expect(result.current.page).toBe(1);
    expect(result.current.search).toBe("");
    expect(result.current.filters).toEqual({});
  });

  it("ramène en page 1 quand la recherche change", () => {
    const { result } = renderHook(() => useListing({ page: 3 }));

    act(() => result.current.setSearch("FR76"));

    expect(result.current.search).toBe("FR76");
    expect(result.current.page).toBe(1);
  });

  it("coche puis décoche une valeur de facette, en revenant en page 1", () => {
    const { result } = renderHook(() => useListing({ page: 2 }));

    act(() => result.current.toggle("currency", "EUR"));

    expect(result.current.filters).toEqual({ currency: ["EUR"] });
    expect(result.current.page).toBe(1);

    act(() => result.current.toggle("currency", "EUR"));

    expect(result.current.filters).toEqual({ currency: [] });
  });

  it("change de page sans toucher au reste", () => {
    const { result } = renderHook(() => useListing({ search: "FR" }));

    act(() => result.current.setPage(4));

    expect(result.current.page).toBe(4);
    expect(result.current.search).toBe("FR");
  });
});
