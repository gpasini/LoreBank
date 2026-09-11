import { act, render } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { FakeEventSource, stubEventSource } from "../test/eventSource";
import { SignalsProvider, useSignals } from "./SignalsProvider";

function Subscriber({
  kind,
  id,
  refresh,
}: {
  kind: string;
  id: string | null;
  refresh: () => void;
}) {
  useSignals(kind, id, refresh);

  return null;
}

const signal = (resourceKind: string, resourceId: string) => ({
  discriminant: "bank.money-deposited",
  resourceKind,
  resourceId,
  occurredAt: "2026-09-11T10:00:00Z",
});

describe("SignalsProvider", () => {
  beforeEach(() => stubEventSource());

  it("ouvre un seul flux sur /api/signals", () => {
    render(
      <SignalsProvider>
        <Subscriber kind="bank-account" id={null} refresh={vi.fn()} />
        <Subscriber kind="bank-account" id="1" refresh={vi.fn()} />
      </SignalsProvider>,
    );

    expect(FakeEventSource.instances).toHaveLength(1);
    expect(FakeEventSource.latest().url).toBe("http://api.test/api/signals");
  });

  it("relit l'abonné à la ressource signalée, et lui seul", () => {
    const one = vi.fn();
    const other = vi.fn();
    const all = vi.fn();
    const elsewhere = vi.fn();

    render(
      <SignalsProvider>
        <Subscriber kind="bank-account" id="1" refresh={one} />
        <Subscriber kind="bank-account" id="2" refresh={other} />
        <Subscriber kind="bank-account" id={null} refresh={all} />
        <Subscriber kind="journal-entry" id={null} refresh={elsewhere} />
      </SignalsProvider>,
    );

    act(() => FakeEventSource.latest().emit(signal("bank-account", "1")));

    expect(one).toHaveBeenCalledTimes(1);
    expect(all).toHaveBeenCalledTimes(1);
    expect(other).not.toHaveBeenCalled();
    expect(elsewhere).not.toHaveBeenCalled();
  });

  it("fait tout relire après une coupure, à la reconnexion", () => {
    const refresh = vi.fn();

    render(
      <SignalsProvider>
        <Subscriber kind="bank-account" id="1" refresh={refresh} />
      </SignalsProvider>,
    );

    act(() => FakeEventSource.latest().reopen());

    expect(refresh).not.toHaveBeenCalled();

    act(() => {
      FakeEventSource.latest().drop();
      FakeEventSource.latest().reopen();
    });

    expect(refresh).toHaveBeenCalledTimes(1);
  });

  it("ferme le flux et oublie l'abonné démonté", () => {
    const refresh = vi.fn();

    const { unmount } = render(
      <SignalsProvider>
        <Subscriber kind="bank-account" id="1" refresh={refresh} />
      </SignalsProvider>,
    );

    unmount();

    expect(FakeEventSource.latest().closed).toBe(true);
  });
});
