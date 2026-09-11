# Le front est un modèle, pas une vitrine

> Statut : accepté — 2026-09-11. Tranche la question laissée ouverte par la
> revue #5 (« socle ou exemple »), et donne au front le Gel que l'ADR 0031
> lui avait délibérément refusé faute de runner.

Le front était « le premier usage de la Description » (ADR 0019), et rien
de plus n'était dit. Pas de statut, pas de frontière, pas de test — le seul
pan du repo sans filet, face à une soixantaine de gardes d'architecture et
de contrat côté back. `npm run build` n'était pas une porte : la CI faisait
`generate`, `typecheck`, `check`, `audit`, et une résolution que `tsc`
accepte et que Vite refuse serait passée. Biome était au défaut, deux
`biome-ignore` vivaient dans le code sans que rien ne les compte. Aucune
skill : un agent qui ajoute une page n'avait pas de procédure, alors qu'il
en avait une pour un value object. Et `CLAUDE.md`, depuis l'ADR 0034, ne
disait plus rien du front — parce qu'il n'y avait rien de tenu à router.

La revue « Cadrer l'IA » (#26) a posé la question autrement : où un agent
produira-t-il le plus de volume visible ? Dans le front. Et c'est là que
rien ne le rattrapait.

## La décision

> Le front **est un modèle** : un cloneur y bâtit son UI. Il a donc, comme
> le back, une frontière écrite, des tests, ses conventions tenues par un
> scan, son Gel, ses portes, et sa skill.

### La frontière, par dossier

| Socle — le cloneur le garde | Exemple — le cloneur le remplace |
|---|---|
| `src/api/` : le Client, `translate`, `faultyFields`, la table `errorMessages` (sa forme ; son contenu suit les modules) | `src/components/` : les écrans de Bank et Ledger |
| `src/listing/` : les briques d'une Liste (ADR 0027) | `src/App.tsx` : l'assemblage |
| `src/signals/` : le flux de Signaux et `useSignals` (ADR 0026) | `src/styles.css` |
| `src/format.ts` : montants, IBAN, Instants dans la locale | |
| `src/test/` : les doubles et le setup | |

La frontière existait déjà dans le graphe d'imports ; elle est maintenant
écrite (`docs/front.md`) et tenue (`conventions.test.ts`). Elle n'est pas
matérialisée par un dossier `socle/` ni un paquet : un déplacement de tous
les imports pour un template, sans gain doctrinal.

### Les tests, à trois niveaux

Vitest sur jsdom, avec Testing Library, colocalisés (`*.test.ts(x)`),
imports explicites — pas de globals. Trois niveaux, chacun avec son
exemple :

1. **pur** : `translate`, `faultyFields`, `format`, `useListing` ;
2. **brique de socle** : `SignalsProvider` (abonnement, filtrage par
   ressource, relecture de tout après une coupure), `SearchBox` (debounce),
   `Facets`, `Pager` ;
3. **écran** : `AccountDetail` — lit par son `GET`, relit après une
   commande et prévient le parent sans rien lire de la réponse (le CQS
   jusqu'à l'écran), affiche l'erreur métier traduite, relit sur Signal.

Deux doubles dans `src/test/`, et aucune dépendance de plus. **L'API est
stubée par un middleware du Client** (`stubApi`) : `openapi-fetch` laisse
`onRequest` rendre une `Response`, ce qui court-circuite le réseau sans
rien injecter dans le code testé — le Client capture `fetch` à sa création,
un stub du global arrivait trop tard. **Le flux de Signaux par un
`EventSource` factice** piloté par le test. Le setup nettoie les deux après
chaque test.

### Les conventions, par un scan

`conventions.test.ts` lit les sources — le pendant de
`DomainConventionTest` — et tient trois règles : le réseau ne s'appelle que
depuis le socle (`openapi-fetch` dans `src/api/`, `fetch` et `EventSource`
dans `src/api/` et `src/signals/`) ; le socle n'importe jamais l'exemple ;
un composant n'importe pas `App`. La troisième a rougi sur un cas réel —
`AccountDetail` importait `bankAccountKind` depuis `App`, qui l'importait en
retour — avant que la constante ne descende dans le composant.

Une quatrième règle reste à la relecture : les types de l'API viennent du
Client, aucun type écrit à la main ne redécrit une réponse. Elle ne se
teste pas proprement ; angle mort écrit.

### Le Gel du front, en vitest

L'ADR 0031 avait exclu le front du Gel : lui coller une garde .NET qui lit
du `.tsx` aurait installé une dépendance à rebours. Le front a maintenant
son runner, donc son Gel, dans son langage : `gel.test.ts` épingle la liste
des `biome-ignore` (deux, par fichier et par règle), refuse tout
`@ts-ignore`/`@ts-expect-error`/`@ts-nocheck` et tout `.skip`/`.only`/
`.todo`, gèle les règles de `biome.json` (liste vide : le défaut de Biome
est la règle, et une règle s'ajoute avec son pourquoi), ses inclusions et
l'absence d'`overrides`, et les trois flags stricts du `tsconfig`. Les
**portes** front, elles, restent gelées depuis la racine par
`QualityGateFreezeTest` : une seule liste pour une vérité.

### Deux portes de plus

`//frontend:test` et `//frontend:build`, entre `check` et `audit`.
`build` devient `vite build` seul : `typecheck` est déjà une porte, et une
porte fait une chose.

### La skill et la doctrine

`nouvel-ecran` : un écran qui lit, s'abonne à sa ressource, envoie ses
commandes et relit, affiche ses erreurs, avec son test sur le stub — dans
l'ordre de l'ADR 0032. `docs/front.md` dit comment on fait ; le README du
front dit comment on lance ; `CLAUDE.md` route les deux.

## Garde-fous

| Règle | Ce qui rougit si elle casse |
|---|---|
| Le réseau ne s'appelle que depuis `src/api/` et `src/signals/` | `conventions.test.ts` |
| Le socle n'importe pas l'exemple ; un composant n'importe pas `App` | `conventions.test.ts` |
| Un `biome-ignore`, une suppression TS, un test désarmé, une règle Biome, un flag strict | `gel.test.ts` (le Gel du front) |
| Les portes front, et leur step de CI | `QualityGateFreezeTest` (ADR 0031) |
| Un code d'erreur que la table ne traduit pas | `typecheck` — `errorMessages` est typée sur l'union `ErrorCode` |
| Un écran relit après sa commande, et sur Signal | son test, sur `stubApi` et `FakeEventSource` — `AccountDetail.test.tsx` en est le modèle |
| Ce que `tsc` accepte et que Vite refuse | `//frontend:build` |
| Un type de l'API écrit à la main | **rien** — la relecture ; `docs/front.md` le dit |
| La couverture du front | **rien** — une mesure à poser, issue ouverte, jumelle de l'ADR 0029 |

## Options écartées

- **Le front comme exemple jetable**, avec une procédure de retrait : c'est
  là qu'un agent produit le plus, et le laisser sans filet revenait à
  décider que le template ne tenait la main qu'à moitié.
- **Le socle dans un dossier ou un paquet** : visible, mais un déplacement
  d'imports sans gain — la frontière est dans le graphe, et le test la tient.
- **msw** pour le réseau : une dépendance et des handlers par route, là où
  le Client typé a déjà une couture prévue pour ça.
- **Injecter `fetch` dans le Client** pour le rendre testable : une ligne de
  production pour un besoin de test, alors que le middleware existe.
- **Les globals de vitest** : moins d'imports, mais des noms implicites dans
  un repo qui n'en a nulle part ailleurs — et `cleanup` automatique
  remplacé par un setup explicite.
- **`noRestrictedImports` de Biome** pour la frontière : tient les imports,
  pas `fetch(` ; et deux mécanismes pour trois règles.
- **Le Gel .NET qui lit le front** : la dépendance à rebours que l'ADR 0031
  refusait déjà.
- **La couverture maintenant** : dix lignes quand les tests existent —
  c'est fait —, mais une mesure a sa propre décision (ADR 0029), et le
  chantier était déjà le plus gros de la liste.
