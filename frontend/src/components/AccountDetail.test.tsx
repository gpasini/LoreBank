import { act, fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { SignalsProvider } from "../signals/SignalsProvider";
import { stubApi } from "../test/apiStub";
import { FakeEventSource, stubEventSource } from "../test/eventSource";
import { AccountDetail } from "./AccountDetail";

const id = "5e6a4a2e-0d4c-4f7c-9d2e-2b1a9d3f4c11";

const account = {
  id,
  iban: "FR7630006000011234567890189",
  balance: 100,
  currency: "EUR",
  isClosed: false,
  openedBy: null,
  openedAt: "2026-09-11T10:00:00Z",
};

const emptyPage = {
  items: [],
  page: 1,
  pageSize: 20,
  totalCount: 0,
  facets: [],
};

const routes = () => [
  { method: "GET" as const, path: `/api/bank/accounts/${id}`, body: account },
  {
    method: "GET" as const,
    path: `/api/ledger/bank-accounts/${id}/movements`,
    body: emptyPage,
  },
];

const flush = () => act(async () => {});

describe("AccountDetail", () => {
  beforeEach(() => stubEventSource());

  it("lit le compte par son GET et l'affiche", async () => {
    stubApi(routes());

    render(<AccountDetail accountId={id} onChanged={vi.fn()} />);
    await flush();

    expect(screen.getByText("FR76 3000 6000 0112 3456 7890 189")).toBeDefined();
    expect(screen.getByText("Dépôt")).toBeDefined();
  });

  it("relit le compte après une commande, et prévient le parent — jamais depuis la réponse", async () => {
    const stub = stubApi([
      ...routes(),
      { method: "POST", path: `/api/bank/accounts/${id}/deposits` },
    ]);
    const onChanged = vi.fn();

    render(<AccountDetail accountId={id} onChanged={onChanged} />);
    await flush();

    const form = screen.getByText("Dépôt").closest("form");

    if (form === null) {
      throw new Error("Le formulaire de dépôt manque.");
    }

    fireEvent.change(form.querySelector("input") as HTMLInputElement, {
      target: { value: "20" },
    });
    fireEvent.submit(form);
    await flush();

    const posts = stub.received("POST", `/api/bank/accounts/${id}/deposits`);

    expect(posts).toHaveLength(1);
    expect(posts[0]?.body).toEqual({ amount: 20, currency: "EUR" });
    expect(stub.received("GET", `/api/bank/accounts/${id}`)).toHaveLength(2);
    expect(onChanged).toHaveBeenCalledTimes(1);
  });

  it("affiche l'erreur métier traduite quand une commande est refusée", async () => {
    stubApi([
      ...routes(),
      {
        method: "POST",
        path: `/api/bank/accounts/${id}/withdrawals`,
        status: 422,
        body: {
          title: "Unprocessable Entity",
          status: 422,
          code: "BANK.INSUFFICIENT_BALANCE",
          parameters: { balance: 100, requested: 500, currency: "EUR" },
          traceId: "00-trace-00",
        },
      },
    ]);

    render(<AccountDetail accountId={id} onChanged={vi.fn()} />);
    await flush();

    const form = screen.getByText("Retrait").closest("form");

    if (form === null) {
      throw new Error("Le formulaire de retrait manque.");
    }

    fireEvent.change(form.querySelector("input") as HTMLInputElement, {
      target: { value: "500" },
    });
    fireEvent.submit(form);
    await flush();

    expect(screen.getByRole("alert").textContent).toContain(
      "Solde insuffisant",
    );
  });

  it("relit le compte à chaque Signal de sa ressource", async () => {
    const stub = stubApi(routes());

    render(
      <SignalsProvider>
        <AccountDetail accountId={id} onChanged={vi.fn()} />
      </SignalsProvider>,
    );
    await flush();

    act(() =>
      FakeEventSource.latest().emit({
        discriminant: "bank.money-deposited",
        resourceKind: "bank-account",
        resourceId: id,
        occurredAt: "2026-09-11T10:00:00Z",
      }),
    );
    await flush();

    expect(stub.received("GET", `/api/bank/accounts/${id}`)).toHaveLength(2);
  });
});
