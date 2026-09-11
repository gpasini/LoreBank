import { describe, expect, it } from "vitest";

// Les conventions du front (docs/front.md, ADR 0036), tenues par un scan des
// sources — le pendant de DomainConventionTest et ApplicationConventionTest
// côté back. Les tests et leurs doubles (src/test/) sont hors du scan : ils
// ne sont pas l'application.
const sources = Object.entries(
  import.meta.glob<string>("/src/**/*.{ts,tsx}", {
    query: "?raw",
    import: "default",
    eager: true,
  }),
)
  .filter(([path]) => !path.startsWith("/src/test/"))
  .filter(([path]) => !/\.test\.tsx?$/.test(path))
  .filter(([path]) => !path.endsWith(".d.ts"));

const socle = ["/src/api/", "/src/listing/", "/src/signals/", "/src/format.ts"];

const isSocle = (path: string) =>
  socle.some((prefix) => path.startsWith(prefix));

const linesMatching = (pattern: RegExp) =>
  sources.flatMap(([path, content]) =>
    content
      .split("\n")
      .map((line, index) => ({ path, line: index + 1, text: line.trim() }))
      .filter(({ text }) => pattern.test(text)),
  );

const where = (hits: { path: string; line: number }[]) =>
  hits.map((hit) => `${hit.path}:${hit.line}`);

describe("les conventions du front", () => {
  it("n'appelle le réseau que depuis le socle : openapi-fetch dans src/api/, fetch et EventSource dans src/api/ et src/signals/", () => {
    const clients = linesMatching(/from "openapi-fetch"/).filter(
      ({ path }) => !path.startsWith("/src/api/"),
    );
    const raw = linesMatching(/\bfetch\(|new EventSource\(/).filter(
      ({ path }) =>
        !path.startsWith("/src/api/") && !path.startsWith("/src/signals/"),
    );

    expect(
      where([...clients, ...raw]),
      "le Client typé (src/api/client.ts) est le seul point d'entrée vers l'API ; un écran importe `api`, jamais fetch",
    ).toEqual([]);
  });

  it("garde le socle indépendant de l'exemple : api/, listing/, signals/ et format.ts n'importent ni components/ ni App", () => {
    const hits = linesMatching(/from "\.{1,2}\/(components\/|App")/).filter(
      ({ path }) => isSocle(path),
    );

    expect(
      where(hits),
      "le socle se garde sans les modules d'exemple : un cloneur retire components/ et App sans rien casser",
    ).toEqual([]);
  });

  it("fait descendre les dépendances : un composant n'importe pas App", () => {
    const hits = linesMatching(/from "\.{1,2}\/App"/).filter(({ path }) =>
      path.startsWith("/src/components/"),
    );

    expect(
      where(hits),
      "App assemble les composants ; ce qu'un composant partage avec App vit dans le composant, ou plus bas",
    ).toEqual([]);
  });
});
