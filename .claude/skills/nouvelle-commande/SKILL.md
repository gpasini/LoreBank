---
name: nouvelle-commande
description: À utiliser avant d'ajouter un use case d'écriture — une opération métier qui change l'état (créer, muter, clôturer…) exposée en HTTP — ou quand une action de controller s'apprête à renvoyer l'état qu'elle vient de modifier.
---

# Nouvelle commande

## Principe

CQS de bout en bout : une commande mute et **ne renvoie rien** — seule
exception, le `Guid` créé d'une `ICreationCommand`, qui ne sert qu'à bâtir le
`Location`. Le contrat HTTP est la surface Application (ADR 0012) : le body se
lie directement sur le record de la commande, pas de dossier `Contracts/`.

## Recette

1. Dossier par use case : `Commands/<UseCase>/` dans l'Application du module,
   le record et son handler dans des fichiers séparés, namespaces alignés sur
   les dossiers.
2. Le record : `sealed record XxxCommand(…) : ICommand` — ou `ICreationCommand`
   si le use case crée l'agrégat. Propriétés primitives : ce record **est** le
   body HTTP.
3. Le handler : `XxxCommandHandler`, `sealed` — charge l'agrégat par le port de
   repository (`GetRequiredByIdAsync` : l'absence lève déjà la
   `NotFoundException` du module, aucun `?? throw` à écrire), construit les VO
   depuis les primitives, appelle la transition, `SaveAsync`. Une naissance
   appelle la factory de l'agrégat à la place du chargement.
4. L'action, dans le controller du module (dérivé de `ModuleController`) :
   `Task<CommandResult>` par `SendAsync(command)` → 204, ou
   `Task<CreationResult>` par `CreateAsync(command, actionName:
   nameof(GetById))` → 201 + `Location` et corps vide — le type de retour est
   ce que la Description OpenAPI lit, aucun `[ProducesResponseType]` à
   écrire. Sur une route mixte, la route est autoritaire : `command with
   { AccountId = id }` écrase un champ posté en double, et la propriété
   écrasée porte `[property: RouteBound]` sur le record pour sortir du body
   décrit. Le nom de l'action est l'`operationId` du Client : du contrat.
5. Tests d'intégration (`Applications/<Agrégat>/XxxTest.cs`, sur
   `BaseIntegrationTest<XxxWebAppFactory, DbSetup>`) : le cas nominal vérifié
   **par une query** — l'état d'après ne s'obtient que par une lecture — et
   chaque rejet métier avec son exception. Arranges via `DbSetup`, toujours
   `await` : bloquer sous le scope ambiant emballe l'échec en
   `AggregateException`.
6. Une nouvelle route se traverse aussi en HTTP réel : un cas dans le
   `CqsContractTest` du module (204 ou 201 + `Location`, corps vide).
7. Si le use case sert d'arrange à d'autres tests, l'ajouter au `DbSetup` du
   module (`CreateXxxAsync`, via les vrais use cases).
8. Le build de l'hôte a réécrit `backend/openapi/lorebank.json` : relire le
   diff (la nouvelle opération, son body sans la propriété `[RouteBound]`)
   et le commiter avec le changement — la CI échoue s'il manque.

## Exemples de référence

`Commands/DepositMoney/` (mutation, route mixte) et `Commands/OpenBankAccount/`
(création) dans `backend/LoreBank.Bank.Application`, leurs actions dans
`BankAccountsController`, leurs tests dans
`LoreBank.Bank.Test.Infrastructure/Applications/BankAccounts/`.

## Garde-fous

| Règle | Ce qui rougit si elle casse |
|---|---|
| Le handler se résout dans le conteneur de l'hôte | `ModuleCompositionTest` |
| La commande s'exécute dans une transaction ambiante | `TransactionBehaviorTest` (socle), `PipelineWiringTest` (module) |
| Un handler d'event qui échoue annule la commande | `TransactionRollbackTest` |
| 204 / 201 + `Location`, corps vide | `ModuleControllerTest` (socle), `CqsContractTest` (module, de bout en bout) |
| Erreur métier → 422/404 codé | `ErrorContractTest`, `ExceptionCodesTest` du module |
| La Description dit 204 / 201 + `Location`, le body sans la propriété `[RouteBound]` | `DescriptionContractTest` (socle), le diff de `backend/openapi/lorebank.json` (CI) |
| Une nouvelle exception métier est dans l'enum `ErrorCode` du Client | `ErrorCodesDescriptionTest` ; côté front, `npm run typecheck` sur `errorMessages.ts` |

Renommer une propriété du record est un breaking change HTTP que le
compilateur ne voit pas : ce sont les payloads réels de `CqsContractTest` qui
rougissent — d'où l'étape 6 — et le diff du document commité qui le montre en
PR — d'où l'étape 8.

## Pièges

- L'état d'après se lit par un `GET` assumé : une commande qui renverrait la
  ressource redeviendrait une lecture, et sa représentation pourrait diverger
  de celle du `GET` sans que rien ne le signale.
- Une commande traverse un seul module : le `TransactionScope` ambiant ne
  tient pas la frontière (règle d'architecture, pas contrainte technique).
- `ReadCommitted` ne protège pas un lire-modifier-écrire concurrent : le
  remède est un jeton de concurrence optimiste sur l'agrégat, pas un niveau
  d'isolation plus strict.
- Les VO se construisent dans le handler, depuis les primitives du record —
  le record reste liable depuis le JSON.
- Oublier `[RouteBound]` sur une route mixte ne casse rien à l'exécution : le
  document déclare alors un champ requis que le serveur ignore, et le Client
  l'envoie en double. C'est le diff de `lorebank.json` qui le montre.

## Avant de terminer

Build sans warning, tests des étapes 5 et 6 verts : nominal relu par une
query, chaque rejet avec son exception, la route traversée en HTTP réel.
