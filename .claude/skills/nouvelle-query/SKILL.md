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
   traiter dans le controller.
7. Le test du use case (`Applications/<Agrégat>/XxxTest.cs`) : **relire chaque
   champ** du Result après une écriture arrangée par `DbSetup` — le mapping
   colonne → propriété de la config keyless est en chaînes que rien ne
   compile, et `ToView` étant hors migrations rien ne signale la dérive avec
   la table : ce test est son seul garde-fou — plus le cas absence →
   `NotFoundException`.
8. Une nouvelle route `GET` épingle son contrat : l'ensemble exact de ses clés
   JSON dans le `CqsContractTest` du module.

## Exemple de référence

`Queries/GetBankAccountById/` (query + handler + Result colocalisé) dans
`backend/LoreBank.Bank.Application`, `Readers/BankAccountReader.cs` et
`Persistence/ReadRows/BankAccountRow.cs` côté Infrastructure,
`GetBankAccountByIdTest` pour la relecture de tous les champs.

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

## Pièges

- Un besoin de forme wire divergente se règle par une autre query avec son
  propre Result — jamais un record de réponse dans l'Api, jamais un Result
  partagé entre deux queries.
- Une query n'est pas une sonde d'existence : son Result est non nullable, et
  l'interroger « pour voir » lève un 404.
- Matérialiser un agrégat pour afficher est le rôle du repository — une
  lecture requête la row de la table, et `Query` refuse un agrégat.
- Une row par table, pas par query : avant d'en créer une, vérifier qu'elle
  n'existe pas déjà dans `Persistence/ReadRows/`. Pas de SQL brut dans un
  reader — `ModuleSql` est réservé aux migrations de données et à
  l'outbox/inbox (ADR 0018).
- Renommer une propriété du Result est un breaking change HTTP : le
  compilateur n'en dit rien, `CqsContractTest` épingle les clés.

## Avant de terminer

Build sans warning, tests des étapes 7 et 8 verts : chaque champ du Result
relu avec sa valeur arrangée, l'absence levant la `NotFoundException` du
module, les clés JSON de la route épinglées.
