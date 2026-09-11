import { describe, expect, it } from "vitest";

// Le Gel du front (ADR 0031, 0036) : les échappatoires se gardent
// elles-mêmes. Un agent coincé sur un lint a un chemin plus court que
// corriger le code — un biome-ignore, un @ts-expect-error, un .skip, une
// règle éteinte dans biome.json, un flag strict retiré du tsconfig. Ces
// assertions rendent le geste explicite : la liste gelée bouge avec le code
// qu'elle couvre, ou rien ne passe. Le scan est textuel ; ce fichier porte
// ses propres motifs et marque ces lignes-là (« gel:motif ») pour ne pas se
// signaler lui-même.
const marker = "gel:motif";

const sources = Object.entries(
  import.meta.glob<string>("/src/**/*.{ts,tsx}", {
    query: "?raw",
    import: "default",
    eager: true,
  }),
).filter(([path]) => !path.endsWith(".d.ts"));

const configs = import.meta.glob<string>(["/biome.json", "/tsconfig.json"], {
  query: "?raw",
  import: "default",
  eager: true,
});

const readJson = (path: string) => JSON.parse(configs[path] ?? "{}");

const because = (rule: string, geste: string) =>
  `le Gel (ADR 0031) : desserrer une porte est légitime, jamais silencieux — ${rule} ; le geste : ${geste}`;

const linesMatching = (pattern: RegExp) =>
  sources.flatMap(([path, content]) =>
    content
      .split("\n")
      .map((line, index) => ({ path, line: index + 1, text: line }))
      .filter(({ text }) => !text.includes(marker))
      .filter(({ text }) => pattern.test(text)),
  );

// Liste gelée : chaque biome-ignore, par fichier et par règle. Les deux
// d'aujourd'hui sont légitimes — le compteur `version` relance un effet
// sans y être lu (ADR 0026).
const frozenBiomeIgnores = [
  "/src/components/AccountDetail.tsx lint/correctness/useExhaustiveDependencies",
  "/src/components/Ledger.tsx lint/correctness/useExhaustiveDependencies",
];

// Liste gelée : les règles que biome.json élève ou éteint. Vide aujourd'hui,
// le défaut de Biome est la règle ; une ligne s'ajoute ici avec son pourquoi
// dans docs/front.md.
const frozenBiomeRules: string[] = [];

const frozenBiomeIncludes = ["**", "!src/api/schema.d.ts", "!dist"];

const frozenStrictness = {
  strict: true,
  noUncheckedIndexedAccess: true,
  isolatedModules: true,
};

describe("le Gel du front", () => {
  it("épingle chaque biome-ignore, par fichier et par règle", () => {
    const declared = linesMatching(/biome-ignore/) // gel:motif
      .map(({ path, text }) => {
        const rule = /biome-ignore\s+([^\s:]+)/.exec(text)?.[1] ?? "?"; // gel:motif

        return `${path} ${rule}`;
      })
      .sort();

    expect(
      declared,
      because(
        "un biome-ignore de plus est une règle éteinte pour une ligne, sans que rien ne le dise",
        "écrire son pourquoi sur la ligne, puis l'ajouter à la liste gelée de gel.test.ts",
      ),
    ).toEqual([...frozenBiomeIgnores].sort());
  });

  it("ne porte aucune suppression TypeScript", () => {
    const hits = linesMatching(/@ts-(ignore|expect-error|nocheck)/); // gel:motif

    expect(
      hits.map(({ path, line }) => `${path}:${line}`),
      because(
        "un @ts-ignore éteint le typecheck pour une ligne — la porte qui rend le front complet sur les codes d'erreur",
        "corriger le type, ou déclarer la limite dans docs/front.md et la geler ici",
      ),
    ).toEqual([]);
  });

  it("ne porte aucun test désarmé", () => {
    const hits = linesMatching(/\b(it|test|describe)\.(skip|only|todo)\(/); // gel:motif

    expect(
      hits.map(({ path, line }) => `${path}:${line}`),
      because(
        "un .skip ou un .only laisse la suite verte en n'exécutant pas ce qu'elle promet",
        "retirer le modificateur, ou supprimer le test avec son pourquoi",
      ),
    ).toEqual([]);
  });

  it("garde biome.json à son défaut, sous sa liste gelée", () => {
    const biome = readJson("/biome.json");
    const rules = Object.entries(biome.linter?.rules ?? {}).flatMap(
      ([group, entries]) =>
        Object.entries(entries as Record<string, unknown>).map(
          ([rule, level]) => `${group}/${rule} = ${JSON.stringify(level)}`,
        ),
    );

    expect(
      biome.linter?.enabled,
      because("le linter éteint est le desserrage maximal", "le rallumer"),
    ).toBe(true);
    expect(
      biome.formatter?.enabled,
      because("le formatter éteint est le desserrage maximal", "le rallumer"),
    ).toBe(true);
    expect(
      biome.overrides,
      because(
        "un override est une règle éteinte pour un dossier",
        "l'écrire en règle gelée, ou en biome-ignore gelé",
      ),
    ).toBeUndefined();
    expect(
      rules,
      because(
        "une règle éteinte dans biome.json l'est pour tout le front, en silence",
        "éditer biome.json avec son pourquoi dans docs/front.md, puis la liste gelée de gel.test.ts",
      ),
    ).toEqual(frozenBiomeRules);
    expect(
      biome.files?.includes,
      because(
        "un fichier exclu de Biome n'est plus ni linté ni formaté",
        "éditer biome.json, puis la liste gelée de gel.test.ts",
      ),
    ).toEqual(frozenBiomeIncludes);
  });

  it("garde le tsconfig strict", () => {
    const options = readJson("/tsconfig.json").compilerOptions ?? {};
    const declared = Object.fromEntries(
      Object.keys(frozenStrictness).map((flag) => [flag, options[flag]]),
    );

    expect(
      declared,
      because(
        "un flag strict retiré desserre le typecheck pour tout le front",
        "éditer tsconfig.json avec son pourquoi, puis gel.test.ts",
      ),
    ).toEqual(frozenStrictness);
  });
});
