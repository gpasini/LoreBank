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
   appelle la factory de l'agrégat à la place du chargement. Les contextes de
   la requête ne sont jamais des champs du record : l'Acteur vient du port
   `ICurrentActor` (ADR 0023) et l'Instant de `TimeProvider` (ADR 0024,
   `GetUtcNow()`), pris en dépendance selon le besoin et passés à la
   transition — voir `OpenBankAccountCommandHandler`. Un handler
   d'integration event fait de même pour l'Instant qu'il comptabilise
   (`MoneyDepositedIntegrationEventHandler` côté Ledger).
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
   chaque rejet métier avec son exception. Arranges par le scénario du
   `DbSetup` (ADR 0030) : `await DbSetup.CreateXxx().Yyy(b => b.With…())
   .RunAsync()` — les gestes empilent, seul le terminal est attendu ; jamais
   de `.Result` sous le scope ambiant, qui emballe l'échec en
   `AggregateException`. Un test qui attend un Acteur ou un Instant précis
   les pose sur les fakes avant l'arrange (`ConfigurableCurrentActor` du
   module, `Factory.TimeProvider.Instant` du socle) et relit exactement
   cette valeur — jamais « autour de maintenant » ; `ResetFakes` efface.
6. Une nouvelle route se traverse aussi en HTTP réel : un cas dans le
   `CqsContractTest` du module (204 ou 201 + `Location`, corps vide).
7. Le builder et le geste de la commande (ADR 0030) : un
   `<Commande>Builder` dans `Test.Infrastructure/Builders/` — champs privés
   à défauts valides, `With<Propriété>()`, le prérequis (l'agrégat visé) en
   nullable lisible posé par `Of(id)`, `Build()` qui lève s'il manque — et
   un geste au présent nu, sans `Async`, dans la partielle
   `DbSetup.<Agrégat>.cs` : `Enqueue(nameof(Geste), step)`, le builder
   configuré et le prérequis comblé **dans l'étape** (le dernier créé, ou un
   par défaut), envoi par `Sender`, `return this`. Une création empile son
   id pour `GetLast<Agrégat>Id()`.
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
| Deux commandes concurrentes sur le même agrégat : la seconde est refusée en 409 `CONCURRENT_UPDATE`, rien à écrire dans le handler (ADR 0020) | `ModuleRepositoryTest` (socle), `ConcurrentUpdateTest` (module de référence) |
| La Description dit 204 / 201 + `Location`, le body sans la propriété `[RouteBound]` | `DescriptionContractTest` (socle), le diff de `backend/openapi/lorebank.json` (CI) |
| Une nouvelle exception métier est dans l'enum `ErrorCode` du Client | `ErrorCodesDescriptionTest` ; côté front, `npm run typecheck` sur `errorMessages.ts` |
| L'Application demande l'Instant à `TimeProvider`, jamais à l'horloge (ADR 0024) | le build : l'analyseur d'API bannies rougit en `RS0030` dans tout projet `.Application` |
| Le scénario du `DbSetup` : étapes dans l'ordre, prérequis comblé à l'exécution, échec enveloppé, accesseurs gardés (ADR 0030) | `DbSetupBaseTest` (socle) ; un `RunAsync()` non attendu est un CS4014 tenu en erreur ; un scénario jamais joué rougit au TearDown |

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
