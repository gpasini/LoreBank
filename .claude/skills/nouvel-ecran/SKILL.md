---
name: nouvel-ecran
description: À utiliser avant d'ajouter ou de modifier un écran du front — un composant qui lit une ressource ou une Liste, envoie des commandes, affiche des erreurs — ou quand un composant s'apprête à appeler fetch, à redéclarer un type de l'API, ou à lire l'état d'après dans la réponse d'une commande.
---

# Nouvel écran

## Principe

Un écran lit par le Client généré, relit après chacune de ses commandes —
jamais depuis la réponse —, s'abonne à sa ressource par le Signal, et
affiche ses erreurs par leur code traduit. Il ne connaît aucun type de
l'API qu'il n'ait pris dans le Client. Son test tourne sur les doubles du
socle (`docs/front.md`, ADR 0036).

## Recette

1. **Le squelette** : `src/components/<Ecran>.tsx`, une fonction qui reçoit
   ses props (l'identifiant de la ressource, `onChanged` pour prévenir le
   parent) et rend `null`. Ses types viennent du Client :
   `type Xxx = components["schemas"]["XxxResult"]`. Juste de quoi compiler.
2. **Le test de l'écran**, écrit maintenant contre ce vide
   (`<Ecran>.test.tsx`, à côté) : `stubApi([...])` déclare le `GET` de la
   ressource et les `POST` des commandes ; le test affirme que l'écran
   affiche ce que le `GET` a rendu, qu'après une commande il a **relu**
   (`received("GET", path)` compte deux) et **prévenu** le parent, qu'une
   commande refusée affiche l'alerte traduite (`getByRole("alert")`), et
   — dans `<SignalsProvider>` avec `stubEventSource()` — qu'un Signal de sa
   ressource le fait relire. **Il doit rougir** — le RED de l'ADR 0032 —
   avant la première ligne du corps.
3. **La lecture** : `useState` pour la ressource et le `ApiProblem`,
   `useEffect` qui appelle `api.GET(...)` et pose `data ?? null` /
   `error ?? null`, avec un `cancelled` pour la course au démontage. Un
   compteur `version` dans les dépendances de l'effet relance la lecture ;
   il n'est lu nulle part — c'est le cas du `biome-ignore` gelé, à recopier
   avec son pourquoi et à ajouter à la liste de `gel.test.ts`.
4. **L'abonnement** : `useSignals(kind, id, () => setVersion(v => v + 1))`.
   Le `kind` est le genre stable du jumeau publié (`ResourceKind` de
   l'integration event) ; il se déclare et s'exporte depuis l'écran qui s'y
   abonne, jamais depuis `App`.
5. **Les commandes** : `api.POST(...)`, puis si `!error` : incrémenter
   `version` et appeler `onChanged()`. La réponse ne se lit pas — sauf le
   `Location` d'une création, dont on prend l'identifiant.
6. **Les erreurs** : `<Problem problem={error} />` pour l'afficher ;
   `faultyFields(error)` pour marquer `aria-invalid` les champs d'un 400.
   Un code nouveau ne compile pas tant que `src/api/errorMessages.ts` ne le
   traduit pas : y ajouter sa ligne, formatée par `format.ts`, jamais un
   paramètre brut.
7. **Une Liste** : `useListing()` pour l'état, `SearchBox`, `Facets` avec
   ses libellés (nommés comme les filtres de la query), `Pager` ; la query
   reçoit `page`, `search || undefined` et les filtres tels quels.
   `Ledger.tsx` est l'exemple sous une ressource, `App.tsx` en tête.
8. **L'assemblage** dans `App.tsx`, qui importe l'écran — jamais l'inverse.
9. `mise run format` depuis `frontend/` (Biome), puis `mise run check` à
   la racine : `typecheck`, Biome, `test` — conventions et Gel compris —,
   `build`.

## Exemple de référence

`src/components/AccountDetail.tsx` et `AccountDetail.test.tsx` : lecture,
trois commandes, relecture, `onChanged`, Signal, erreur traduite.
`src/components/OpenAccountForm.tsx` pour une création (201 + `Location`).
`src/App.tsx` pour une Liste, `src/components/Ledger.tsx` pour une Liste
sous une ressource.

## Garde-fous

| Règle | Ce qui rougit si elle casse |
|---|---|
| Un écran n'appelle pas `fetch` ni `new EventSource` : il passe par `api` et `useSignals` | `conventions.test.ts` |
| Un composant n'importe pas `App` ; le socle n'importe pas `components/` | `conventions.test.ts` |
| Un code d'erreur que `errorMessages` ne traduit pas | `typecheck` — la table est typée sur l'union `ErrorCode` |
| Un chemin ou un paramètre qui n'existe pas dans la Description | `typecheck` — le Client est typé sur `paths` |
| L'écran relit après sa commande, prévient le parent, relit sur Signal, affiche l'erreur traduite | son test — étape 2 |
| Un `biome-ignore` de plus, un `@ts-expect-error`, un `.only` | `gel.test.ts` (le Gel du front) |
| Ce que `tsc` accepte et que Vite refuse | `//frontend:build` |
| Un type de l'API redéclaré à la main | **rien** — la relecture (`docs/front.md`) |

## Pièges

- Lire l'état d'après dans la réponse d'une commande : elle n'en a pas
  (204, ou 201 sans corps). Le `GET` est la seule vérité, et le test de
  l'étape 2 compte les `GET`.
- Un `type Account = { id: string; … }` écrit à la main : il compile, il
  diverge en silence. `components["schemas"]` est la seule source.
- Le `kind` d'une ressource dans `App` : un composant l'importerait en
  remontant, et `conventions.test.ts` rougit.
- Stubber `globalThis.fetch` : le Client l'a capturé à sa création, le stub
  arrive trop tard. `stubApi` est un middleware du Client.
- Une valeur de facette ou un montant affiché brut : `format.ts` et les
  libellés de `Facets` traduisent dans la locale.

## Avant de terminer

Le test de l'étape 2 **rouge d'abord, puis** vert ; `mise run check` à la
racine vert — `typecheck`, Biome, tests (conventions et Gel compris), build.
