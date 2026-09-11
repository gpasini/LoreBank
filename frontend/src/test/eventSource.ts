import { vi } from "vitest";

// Le double d'EventSource : le flux de Signaux (ADR 0026) piloté par le
// test — un message, une coupure, une reconnexion.
export class FakeEventSource {
  static instances: FakeEventSource[] = [];

  onmessage: ((event: MessageEvent<string>) => void) | null = null;
  onerror: (() => void) | null = null;
  onopen: (() => void) | null = null;
  closed = false;

  constructor(readonly url: string) {
    FakeEventSource.instances.push(this);
  }

  emit(data: unknown) {
    this.onmessage?.(
      new MessageEvent("message", { data: JSON.stringify(data) }),
    );
  }

  drop() {
    this.onerror?.();
  }

  reopen() {
    this.onopen?.();
  }

  close() {
    this.closed = true;
  }

  static latest(): FakeEventSource {
    const latest = FakeEventSource.instances.at(-1);

    if (latest === undefined) {
      throw new Error("Aucun EventSource ouvert.");
    }

    return latest;
  }
}

export function stubEventSource() {
  FakeEventSource.instances = [];
  vi.stubGlobal("EventSource", FakeEventSource);
}
