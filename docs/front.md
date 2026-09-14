# Le front : la frontière, les tests, le Gel

Comment on écrit dans `frontend/` — ce qui est du socle, ce qui est de
l'exemple, ce que chaque écran fait, et ce qui rougit. Décision et
alternatives : ADR 0036. Lancer, installer : `frontend/README.md`.

## En une phrase

Le front est un **modèle** : un cloneur y bâtit son UI sur le socle
(`api/`, `listing/`, `signals/`, `format.ts`) en remplaçant l'exemple
(`components/`, `App.tsx`). Tout ce qui touche l'API passe par le Client
généré ; un écran relit après sa commande ; ses erreurs sont des codes
traduits ; il s'abonne à sa ressource.

## La frontière

| Dossier | Statut | Rôle |
|---|---|---|
| `src/api/client.ts` | socle | le Client (`openapi-fetch`, typé sur `paths`), `baseUrl`, `ApiProblem`, `ErrorCode` |
| `src/api/problems.ts` | socle | `translate` (un `ApiProblem` → un texte), `faultyFields` (un 400 → les champs à surligner) |
| `src/api/errorMessages.ts` | socle par sa forme | la table `ErrorCode` → message ; son contenu suit les modules — un code de plus ne compile pas tant qu'il n'y est pas |
| `src/api/schema.d.ts` | généré | le Client : jamais édité, jamais commité |
| `src/listing/` | socle | `useListing`, `SearchBox`, `Facets`, `Pager` — les briques d'une Liste (ADR 0027) |
| `src/signals/` | socle | `SignalsProvider` (un `EventSource` par onglet) et `useSignals` (ADR 0026) |
| `src/format.ts` | socle | `money`, `iban`, `instant` — formater dans la locale, jamais côté serveur |
| `src/test/` | socle | `stubApi`, `FakeEventSource`, `setup.ts`, `conventions.test.ts`, `gel.test.ts` |
| `src/components/` | exemple | les écrans de Bank et Ledger |
| `src/App.tsx`, `src/styles.css` | exemple | l'assemblage et l'habillage |
| `src/main.tsx` | entrée | monte `SignalsProvider` puis `App` |

Un cloneur qui retire Bank (#8) retire `components/` et vide
`errorMessages` de ses codes `BANK.*` — le typecheck lui dit lesquels.

## Le Client, seul point d'entrée

`api` (`src/api/client.ts`) est le seul objet qui parle à l'API. Un écran
l'importe et appelle `api.GET("/api/…", { params })` ou `api.POST` ; il
reçoit `{ data, error, response }` typés par la Description. Personne
n'écrit `fetch(` hors de `src/api/` et `src/signals/`, personne ne
redéclare un type de réponse : `components["schemas"]["XxxResult"]` est la
seule source.

## Une commande ne renvoie rien

Le CQS tient jusqu'à l'écran. Après un `POST`, l'écran **relit** par son
`GET` — un compteur `version` relance l'effet de lecture — et **prévient
son parent** (`onChanged`) pour que la Liste se recharge aussi. Il ne lit
rien de la réponse de la commande, sauf le `Location` d'une création
(`OpenAccountForm`). C'est ce que le test de l'écran affirme : deux `GET`
après un `POST`, `onChanged` appelé une fois.

## Les erreurs

Un `error` est un `ApiProblem` : `code` et `parameters` bruts
(`docs/erreurs.md`). `<Problem problem={error} />` l'affiche traduit ;
`faultyFields(error)` donne les champs d'un 400 à marquer `aria-invalid`.
Un code nouveau côté back arrive dans `ErrorCode` à la régénération et
**ne compile pas** tant que `errorMessages` ne le traduit pas : c'est la
porte `typecheck`. On formate ici, avec `format.ts`, jamais on n'affiche un
paramètre brut.

## Le Signal

`useSignals(kind, id, refresh)` abonne l'écran à une ressource — une
instance, ou toutes avec `id` nul — et appelle `refresh` à chaque Signal
de cette ressource, et pour tout le monde après une coupure. Le `kind` est
le genre stable du jumeau publié (`bank-account`) ; il vit avec le
composant qui s'y abonne, et `App` l'importe de là. Le Signal ne remplace
pas la relecture après sa propre commande.

## La Liste

Un écran qui liste assemble `useListing` (page, recherche, filtres — la
recherche ou un filtre ramène en page 1), `SearchBox` (debounce),
`Facets` (avec ses libellés, une valeur cochée disparue reste affichée à
0) et `Pager`. Les paramètres de la query sont ceux du Client
(`currency?: string[]`) ; les valeurs de facettes arrivent en chaînes, le
libellé les traduit. `App.tsx` (la Liste des comptes) et `Ledger.tsx` (une
Liste sous une ressource) sont les deux exemples.

## Les tests

Vitest sur jsdom, Testing Library, colocalisés (`X.test.ts` à côté de
`X.ts`), imports explicites depuis `vitest` — pas de globals. Trois
niveaux : pur (`problems`, `format`, `useListing`), brique de socle
(`SignalsProvider`, `SearchBox`, `Facets`, `Pager`), écran
(`AccountDetail`). Le RED d'abord (ADR 0032) : un test sur un écran neuf
rougit contre le squelette ; un test sur du code existant se prouve en
sabotant ce qu'il spécifie, puis en restaurant.

Les doubles, dans `src/test/` :

- `stubApi(routes)` répond à la place du réseau, par un middleware du
  Client : `{ method, path, status?, body?, headers? }` par route, et
  `received(method, path)` pour compter les appels et lire les corps.
  Un chemin non stubé lève.
- `stubEventSource()` puis `FakeEventSource.latest().emit(signal)`,
  `.drop()`, `.reopen()` pilotent le flux de Signaux.
- `setup.ts` nettoie après chaque test : `cleanup`, éjection des stubs,
  globals restaurés.

`mise run test` ; `npx vitest` pour la boucle.

## Les conventions

`src/test/conventions.test.ts` scanne les sources (hors tests et doubles) :

| Règle | Pourquoi |
|---|---|
| `openapi-fetch` ne s'importe que dans `src/api/` ; `fetch(` et `new EventSource(` n'apparaissent que dans `src/api/` et `src/signals/` | le Client typé est le seul point d'entrée |
| `api/`, `listing/`, `signals/`, `format.ts` n'importent ni `components/` ni `App` | le socle se garde sans l'exemple |
| Un composant n'importe pas `App` | les dépendances descendent |

Et un angle mort assumé : **un type de l'API écrit à la main** (un
`type Account = { … }` qui redécrit un Result) ne se détecte pas
proprement. La relecture le tient.

## Le Gel du front

`src/test/gel.test.ts`, sur le modèle de `QualityGateFreezeTest`
(ADR 0031) : la liste gelée des `biome-ignore` (fichier et règle — deux
aujourd'hui, le compteur `version` qui relance un effet sans y être lu) ;
aucun `@ts-ignore`, `@ts-expect-error`, `@ts-nocheck` ; aucun `.skip`,
`.only`, `.todo` ; `biome.json` au défaut — `linter` et `formatter`
allumés, pas d'`overrides`, ses inclusions gelées, et sa liste de `rules`
gelée, vide aujourd'hui ; `strict`, `noUncheckedIndexedAccess`,
`isolatedModules` dans le `tsconfig` ; la carte de couverture de
`vite.config.ts` — son inclusion, ses exclusions, et aucun seuil — lue en
appelant la config, pas en scannant sa source. Desserrer reste légitime :
le message nomme le fichier à éditer avant la liste, et `docs/front.md`
porte le pourquoi.

## Les portes

Dans l'ordre de `mise run check` : `generate`, `typecheck`, `check`
(Biome), `test`, `build` (`vite build` seul — ce que `tsc` accepte et que
Vite refuse), `audit`. Le registre complet : `docs/qualite.md`. Hors de
`check`, `coverage` rend la carte (`coverage/index.html`) : une mesure,
jamais une porte (ADR 0029, `docs/qualite.md`).
