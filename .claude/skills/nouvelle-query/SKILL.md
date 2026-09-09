---
name: nouvelle-query
description: À utiliser avant d'ajouter un use case de lecture — exposer un état en HTTP (détail, liste, projection) — ou quand une lecture s'apprête à passer par le repository d'un agrégat ou à réutiliser le Result d'une autre query.
---

# Nouvelle query

## Principe

Une query lit et retourne son Result immuable — jamais l'agrégat, jamais
`null` : l'absence est une erreur métier (`NotFoundException` du module),
c'est ce qui garantit qu'un 404 porte toujours un `code`. Un Result appartient
à exactement une query (ADR 0012) et **est** la forme wire du `GET`.

## Recette

1. Dossier `Queries/<UseCase>/` dans l'Application : le record
   `XxxQuery(…) : IQuery<XxxResult>`, son handler, et le Result colocalisé —
   record de primitives, peuplé colonne par colonne, sans dépendance au modèle
   d'écriture.
2. Le port de lecture dans `Readers/` de l'Application (il rend un Result,
   donc il se déclare là) : `Task<XxxResult?> …Async(…)` — pour un lecteur,
   `null` est un résultat normal.
3. Le handler transforme l'absence en erreur :
   `return result ?? throw new XxxNotFoundException(…)`.
4. La row de la table lue, si elle n'existe pas encore : une classe plate de
   primitives dans `Persistence/ReadRows/` (une row par **table**, partagée
   par tous les readers du module — jamais une row par query), et sa config
   `HasNoKey()` + `ToView("<table>")` + `HasColumnName` par propriété dans
   `Persistence/Configurations/`. Le snapshot EF porte la row : générer la
   migration (vide d'opérations) par la skill `nouvelle-migration-schema`.
5. Le reader dans `Readers/` de l'Infrastructure : dérive de `ModuleReader`,
   compose `Where`/`OrderBy` sur `Query<XxxRow>()` et projette vers le Result
   dans le `Select` final — EF ne lit que les colonnes touchées, et `Query`
   refuse un agrégat. Un format de valeur (référence, code) reste fabriqué
   par son VO, comparé à la primitive de la row. L'enregistrer dans le
   `Module` Autofac du module.
6. L'action `GET` : `Task<ActionResult<XxxResult>>`,
   `await Sender.Send(…)` — le Result part tel quel, aucun cas d'absence à
   traiter dans le controller. C'est ce type de retour que la Description
   OpenAPI lit (200 + schéma du Result) ; le nom de l'action est
   l'`operationId` du Client — `CreatedAtAction` d'une création le cible
   aussi, le renommer casse les deux.
7. Le test du use case (`Applications/<Agrégat>/XxxTest.cs`) : **relire chaque
   champ** du Result après une écriture arrangée par `DbSetup` — le mapping
   colonne → propriété de la config keyless est en chaînes que rien ne
   compile, et `ToView` étant hors migrations rien ne signale la dérive avec
   la table : ce test est son seul garde-fou — plus le cas absence →
   `NotFoundException`.
8. Une nouvelle route `GET` épingle son contrat : l'ensemble exact de ses clés
   JSON dans le `CqsContractTest` du module.
9. Le build de l'hôte a réécrit `backend/openapi/lorebank.json` : relire le
   diff (l'opération, le schéma du Result) et le commiter avec le changement
   — la CI échoue s'il manque.

## Lister

Une lecture qui rend plusieurs éléments est une **Liste** (ADR 0027) — une
seule forme dans le socle, jamais une liste nue ni une enveloppe maison :

1. La query dérive de `ListQuery<XxxItemResult>` (`SharedKernel.Application`)
   au lieu d'implémenter `IQuery<>` : elle hérite `Page`, `PageSize`,
   `Search`, et déclare ses filtres en propriétés typées **multi-valeurs**
   (`public IReadOnlyList<string>? Currency { get; init; }`). Pas de
   paramètre de tri.
2. L'item (`XxxItemResult`) est le Result de la query, colocalisé ; la
   réponse est `ListPage<XxxItemResult>` — l'enveloppe du socle, pas un
   Result à écrire.
3. Le port de lecture reçoit la query entière :
   `Task<ListPage<XxxItemResult>> ListAsync(ListXxxQuery query, ct)` — une
   Page, jamais `null` : une liste n'a pas d'absence, le handler la rend
   telle quelle.
4. Le reader déclare, le moteur exécute :

   ```csharp
   Query<XxxRow>()
       .List(query)
       .SearchIn(row => row.Label)
       .Filter(query.Currency, row => row.Currency, facet: nameof(query.Currency))
       .OrderBy(row => row.Label)
       .ToPageAsync(row => new XxxItemResult(…), cancellationToken)
   ```

   `SearchIn` répétable (OU entre colonnes), `Filter` sans `facet:` pour
   un filtre sans facette, `OrderBy` obligatoire.
5. L'action : `List([FromQuery] ListXxxQuery query, ct)` rendant
   `Task<ActionResult<ListPage<XxxItemResult>>>`, `await Sender.Send(query)`.
   Sous une ressource (`GET …/{id}/movements`) : la query porte
   `[RouteBound] Guid AccountId` (scalaire, ce n'est pas un filtre), le
   controller envoie `query with { AccountId = id }`, et le handler vérifie
   la ressource avant de lire — son absence est le 404 du module, jamais
   une Page vide (voir `ListLedgerMovements`).
6. Le test du use case relit chaque champ de l'item par la recherche, et
   affirme les facettes sous le nom des filtres ; la mécanique (bornes,
   jokers, facettes disjonctives) est prouvée par le socle
   (`ListContractTest`), pas à refaire. `CqsContractTest` épingle les clés
   de la Page et de l'item.
7. Le front assemble les briques de `frontend/src/listing/` (`useListing`,
   `SearchBox`, `Facets`, `Pager`) pour sa query, avec ses libellés de
   facettes (`App.tsx`, la Liste des comptes, est l'exemple).

## Exemple de référence

`Queries/GetBankAccountById/` (query + handler + Result colocalisé) dans
`backend/LoreBank.Bank.Application`, `Readers/BankAccountReader.cs` et
`Persistence/ReadRows/BankAccountRow.cs` côté Infrastructure,
`GetBankAccountByIdTest` pour la relecture de tous les champs. Pour une
Liste : `Queries/ListBankAccounts/` et `ListBankAccountsTest`.

## Garde-fous

| Règle | Ce qui rougit si elle casse |
|---|---|
| Le handler se résout dans le conteneur de l'hôte | `ModuleCompositionTest` |
| La query ne passe pas par le repository de l'agrégat | `ApplicationConventionTest` |
| Aucune transaction ambiante autour d'une lecture | `PipelineWiringTest` (module), `TransactionBehaviorTest` (socle) |
| Un reader ne requête que des rows keyless, jamais un agrégat | `ModuleReaderTest` (`Query` refuse un type à clé ou hors modèle) |
| La row est dans le snapshot (migration générée) | `ModuleCompositionTest` (`HasPendingModelChanges`) |
| Le 404 d'une lecture porte un code | `ErrorContractTest`, plus le cas absence de l'étape 7 |
| L'ensemble exact des clés JSON du `GET` | `CqsContractTest` — étape 8 |
| La Description dit 200 + le schéma du Result, `decimal` en `number` | `DescriptionContractTest` (socle), le diff de `backend/openapi/lorebank.json` (CI) |
| Une query qui rend une Page dérive de `ListQuery`, ses filtres sont multi-valeurs | `ApplicationConventionTest` |
| La mécanique de la Liste — bornes (422 `INVALID_PAGING`), recherche, OU/ET, facettes disjonctives, paramètres camelCase | `ListContractTest`, `DescriptionContractTest` (socle, sur le Probe) |

## Pièges

- Un besoin de forme wire divergente se règle par une autre query avec son
  propre Result — jamais un record de réponse dans l'Api, jamais un Result
  partagé entre deux queries.
- Une query n'est pas une sonde d'existence : son Result est non nullable, et
  l'interroger « pour voir » lève un 404.
- Matérialiser un agrégat pour afficher est le rôle du repository — une
  lecture requête la row de la table, et `Query` refuse un agrégat.
- Une liste sans borne, ou une enveloppe de pagination écrite dans le
  module : la Liste est la forme du socle (ADR 0027) — `ListQuery`,
  `ListPage`, le moteur — et `ApplicationConventionTest` rougit sur une
  query qui rend une Page sans dériver de `ListQuery`.
- Une row par table, pas par query : avant d'en créer une, vérifier qu'elle
  n'existe pas déjà dans `Persistence/ReadRows/`. Pas de SQL brut dans un
  reader — `ModuleSql` est réservé aux migrations de données et à
  l'outbox/inbox (ADR 0018).
- Renommer une propriété du Result est un breaking change HTTP : le
  compilateur n'en dit rien, `CqsContractTest` épingle les clés et le diff du
  document commité le montre en PR. Renommer l'action renomme l'`operationId`
  — le Client casse à la régénération.

## Avant de terminer

Build sans warning, tests des étapes 7 et 8 verts : chaque champ du Result
relu avec sa valeur arrangée, l'absence levant la `NotFoundException` du
module, les clés JSON de la route épinglées.
