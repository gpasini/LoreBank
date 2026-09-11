import { describe, expect, it } from "vitest";
import { iban, instant, money } from "./format";

describe("money", () => {
  it("formate un montant dans sa devise", () => {
    expect(money(20.5, "EUR")).toMatch(/20[.,]50?\s?€|€\s?20[.,]50/);
  });

  it("retombe sur le montant brut quand la devise est inconnue", () => {
    expect(money(20.5, "???")).toBe("20.5 ???");
  });
});

describe("iban", () => {
  it("groupe par quatre", () => {
    expect(iban("FR7630006000011234567890189")).toBe(
      "FR76 3000 6000 0112 3456 7890 189",
    );
  });
});

describe("instant", () => {
  it("affiche un Instant ISO dans la locale du navigateur", () => {
    expect(instant("2026-09-11T10:00:00Z")).toMatch(/2026/);
  });

  it("rend la valeur telle quelle quand elle n'est pas une date", () => {
    expect(instant("pas une date")).toBe("pas une date");
  });
});
