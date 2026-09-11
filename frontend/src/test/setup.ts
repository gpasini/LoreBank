import { cleanup } from "@testing-library/react";
import { afterEach, vi } from "vitest";
import { ejectStubs } from "./apiStub";

// Sans les globals de vitest, Testing Library ne se nettoie pas seule ; et
// un stub d'API ou d'EventSource ne survit pas à son test.
afterEach(() => {
  cleanup();
  ejectStubs();
  vi.unstubAllGlobals();
});
