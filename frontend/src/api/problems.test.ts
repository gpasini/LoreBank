import { describe, expect, it } from "vitest";
import type { ApiProblem } from "./client";
import { faultyFields, translate } from "./problems";

const problem = (overrides: Partial<ApiProblem>): ApiProblem => ({
  title: "Unprocessable Entity",
  status: 422,
  traceId: "00-trace-00",
  ...overrides,
});

describe("translate", () => {
  it("traduit un code par sa table, avec ses paramètres bruts", () => {
    const text = translate(
      problem({
        code: "BANK.INSUFFICIENT_BALANCE",
        parameters: { balance: 10, requested: 20, currency: "EUR" },
      }),
    );

    expect(text).toContain("Solde insuffisant");
    expect(text).toContain("20");
    expect(text).toContain("10");
  });

  it("rend un message générique par statut quand la réponse n'a pas de code", () => {
    expect(translate(problem({ status: 500 }))).toBe(
      "Erreur 500 — réessayez plus tard.",
    );
  });
});

describe("faultyFields", () => {
  it("lit les champs fautifs d'un 400 de binding", () => {
    const fields = faultyFields(
      problem({
        status: 400,
        code: "VALIDATION_FAILED",
        parameters: { fields: ["amount", "currency"] },
      }),
    );

    expect([...fields]).toEqual(["amount", "currency"]);
  });

  it("est vide quand le problème ne porte pas de champs", () => {
    expect(faultyFields(problem({ code: "BANK.ACCOUNT_CLOSED" })).size).toBe(0);
  });
});
