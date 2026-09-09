# Les erreurs, de bout en bout

Comment une règle métier violée dans un agrégat devient un JSON que le front
sait traduire — et ce qui se passe dans tous les autres cas.

Les réponses HTTP de ce document ne sont pas illustratives : elles ont été
relevées sur l'API en marche. Les rares affirmations qui n'ont pas pu être
observées sont signalées comme telles.

## En une phrase

Le back ne produit aucun texte destiné à un humain. Une erreur métier est un
**code** stable et des **paramètres** primitifs ; le front possède les
traductions et fabrique la phrase.

## Le cycle de vie d'une erreur métier

```
BankAccount.Withdraw(…)
  │  l'invariant est violé
  ▼
throw new InsufficientBalanceException(balance, requested)
  │  hérite de DomainException, qui dérive son code de son propre type
  ▼
le handler MediatR remonte l'exception
  │  TransactionBehavior sort de son scope sans Complete() → rien n'est écrit
  ▼
DomainExceptionFilter (LoreBank.SharedKernel.Api)
  │  422, ou 404 si l'exception dérive de NotFoundException
  ▼
{ "code": "BANK.INSUFFICIENT_BALANCE", "parameters": { … } }
```

Trois conséquences à retenir :

- **L'exception annule l'écriture.** Le `TransactionScope` ouvert par le
  `TransactionBehavior` n'est jamais complété, donc la transaction est
  condamnée. Une commande qui échoue ne laisse rien derrière elle.
- **Le filtre est unique et transverse.** Il vit dans
  `LoreBank.SharedKernel.Api`, l'hôte l'enregistre une fois pour tous les
  modules, et il ne connaît aucun module — c'est `NotFoundException`, un type du
  SharedKernel, qui lui fait choisir 404.
- **Le filtre ne se demande pas d'où vient l'exception.** Un handler de commande,
  un value object, un handler de domain event : toute `DomainException` qui
  remonte jusqu'à lui devient un `ProblemDetails`.

## Écrire une exception métier

Une classe par violation, `sealed`, héritant de `DomainException`. Elle ne porte
aucun texte : elle passe à sa base un dictionnaire de paramètres nommés.

```csharp
public sealed class InsufficientBalanceException(
    Money balance,
    Money requested
) : DomainException(
    new() {
        ["balance"] = balance.Amount,
        ["requested"] = requested.Amount,
        ["currency"] = balance.Currency,
    }
);
```

Trois règles, et une seule est évidente :

**Les valeurs sont des primitives, jamais des value objects.** On passe
`balance.Amount` et `balance.Currency`, pas `balance`. Sinon la forme interne des
VO devient un contrat public, et surtout le front reçoit `20.00` au lieu de
`"20.00 EUR"` — c'est lui qui formate selon la locale de l'utilisateur.

**Les clés sont en camelCase.** ASP.NET Core met les *propriétés* en camelCase
mais laisse les clés de dictionnaire telles quelles (`DictionaryKeyPolicy` est
nul) : `["AccountId"]` partirait en `AccountId` dans le JSON.

**Le code se dérive tout seul, ne le déclarez pas.** Il vaut
`<MODULE>.<VIOLATION>` où la violation est le nom de la classe privé du suffixe
`Exception` en SCREAMING_SNAKE_CASE, et le module le 2ᵉ segment du namespace.
Les exceptions du SharedKernel n'ont pas de préfixe.

| Exception | Namespace | Code |
|---|---|---|
| `InvalidIbanException` | `LoreBank.SharedKernel.Domain.Exceptions` | `INVALID_IBAN` |
| `InvalidBicException` | idem | `INVALID_BIC` |
| `InvalidCurrencyException` | idem | `INVALID_CURRENCY` |
| `CurrencyMismatchException` | idem | `CURRENCY_MISMATCH` |
| `InsufficientBalanceException` | `LoreBank.Bank.Domain.Exceptions` | `BANK.INSUFFICIENT_BALANCE` |
| `AccountClosedException` | idem | `BANK.ACCOUNT_CLOSED` |
| `NonEmptyAccountClosureException` | idem | `BANK.NON_EMPTY_ACCOUNT_CLOSURE` |
| `BankAccountNotFoundException` | `LoreBank.Bank.Application.Exceptions` | `BANK.BANK_ACCOUNT_NOT_FOUND` |

**Corollaire à ne pas manquer : renommer une classe d'exception change un
identifiant public.** C'est pour ça qu'un test épingle les huit codes ci-dessus ;
un renommage fait passer la suite au rouge au lieu de casser le front en silence.

Pour une erreur « introuvable », hériter de `NotFoundException` plutôt que de
`DomainException` — c'est ce qui produit un 404 au lieu d'un 422.

`Exception.Message` existe toujours, fabriqué automatiquement à partir du code et
des paramètres en culture invariante
(`BANK.INSUFFICIENT_BALANCE (balance=0, requested=50, currency=EUR)`). Il sert
aux logs et aux stack traces, jamais au client.

## Le contrat HTTP

Un `ProblemDetails` (RFC 9457), servi en `application/problem+json; charset=utf-8`,
avec trois extensions :

```json
{
  "title": "Unprocessable Entity",
  "status": 422,
  "code": "BANK.INSUFFICIENT_BALANCE",
  "parameters": { "balance": 0, "requested": 50, "currency": "EUR" },
  "traceId": "0af7651916cd43dd8448eb211c80319c"
}
```

- `code` — l'identifiant stable que le front traduit.
- `parameters` — objet plat de primitives, présent même vide.
- `traceId` — la Corrélation (ADR 0022) : l'identifiant de trace W3C que la
  requête porte dans tous ses logs, propagé depuis un `traceparent` entrant.
  Présent sur toute erreur, 500 compris : c'est ce qu'un client cite, et ce
  qu'un exploitant cherche dans les logs. Les exemples plus bas l'omettent
  pour la lisibilité.
- `title` — le libellé standard du statut, jamais un nom de classe C#.
- `detail` — **absent**. Le back ne produisant pas de texte, un `detail` non nul
  ne ferait qu'inviter le front à l'afficher.
- `type` — absent aussi. La RFC 9457 §4.2.1 veut qu'en son absence `title` soit
  le libellé du statut : les deux décisions se tiennent.

**Toutes les erreurs ont cette forme, sans exception** — y compris celles que le
domaine n'a jamais vues passer (400 de binding, 500). Une seule réponse d'erreur
échappe au `code` : le 500, qui n'a rien à dire de traduisible.

Trois portes de sortie produisent ces réponses, et une seule classe leur donne
leur forme :

| Fichier (`LoreBank.SharedKernel.Api`) | Rôle |
|---|---|
| `Problems/ApiProblem.cs` | la forme commune : `title`, `traceId`, pas de `detail`, type de média |
| `Filters/DomainExceptionFilter.cs` | filtre MVC — les `DomainException` (422 / 404 / 409) |
| `Validation/ValidationProblemFactory.cs` | fabrique d'`[ApiController]` — les 400 de binding |
| `Handlers/UnhandledExceptionHandler.cs` | middleware — tout le reste (500) |

`ApiProblem` existe pour une raison précise : sans lui, trois endroits
décideraient chacun de leur côté ce qu'est une erreur, et divergeraient au
premier ajout.

## Tous les cas

### 422 — un invariant du domaine est violé

Le cas nominal. Un value object refuse une valeur, ou un agrégat refuse une
transition.

```
POST /api/bank/accounts   {"iban":"NOPE","currency":"EUR"}
→ 422
{"title":"Unprocessable Entity","status":422,
 "code":"INVALID_IBAN","parameters":{"iban":"NOPE"}}
```

```
POST /api/bank/accounts/{id}/withdrawals   {"amount":50,"currency":"EUR"}
→ 422
{"title":"Unprocessable Entity","status":422,
 "code":"BANK.INSUFFICIENT_BALANCE",
 "parameters":{"balance":0,"requested":50,"currency":"EUR"}}
```

```
POST /api/bank/accounts/{id}/deposits   {"amount":10,"currency":"USD"}
→ 422
{"title":"Unprocessable Entity","status":422,
 "code":"CURRENCY_MISMATCH","parameters":{"left":"EUR","right":"USD"}}
```

```
POST /api/bank/accounts/{id}/deposits   (sur un compte fermé)
→ 422
{"title":"Unprocessable Entity","status":422,
 "code":"BANK.ACCOUNT_CLOSED",
 "parameters":{"accountId":"e9a613d4-cf97-4703-b2cc-6253ac90ff55"}}
```

Noter `"balance":0` : un nombre brut, pas une chaîne formatée. C'est voulu.

### 404 — la ressource n'existe pas

**Un seul 404, quelle que soit la porte d'entrée.** Que l'absence soit constatée
par une commande ou par une lecture, la réponse est la même — même statut, même
code, mêmes paramètres :

```
POST /api/bank/accounts/00000000-…-000000000000/deposits
→ 404
{"title":"Not Found","status":404,
 "code":"BANK.BANK_ACCOUNT_NOT_FOUND",
 "parameters":{"accountId":"00000000-0000-0000-0000-000000000000"}}
```

```
GET /api/bank/accounts/00000000-…-000000000000
→ 404
{"title":"Not Found","status":404,
 "code":"BANK.BANK_ACCOUNT_NOT_FOUND",
 "parameters":{"accountId":"00000000-0000-0000-0000-000000000000"}}
```

C'est le handler de la query qui lève, pas le controller. Il n'interroge pas le
repository de l'agrégat mais un **port de lecture**, que l'Infrastructure sert en
tapant la table directement :

```csharp
var result = await reader.GetByIdAsync(
    id: request.AccountId,
    cancellationToken: cancellationToken
);

return result ?? throw new BankAccountNotFoundException(new BankAccountId(request.AccountId));
```

Le port rend `null` — pour un lecteur, une ligne absente est un résultat normal.
C'est l'Application qui décide que c'est une erreur, et c'est le bon endroit :
un port de lecture n'a pas à connaître la politique HTTP de celui qui l'appelle.

**L'absence est une erreur, pas une valeur de retour.** Un `IQuery<T>` rend un
`T` non nullable ; le controller n'a aucun cas d'absence à traiter, donc aucune
occasion d'oublier le code. Le prix de ce choix : une query ne peut plus servir
de sonde d'existence — tester si un compte existe demande un `try`/`catch`, ou
une query dédiée qui rend un booléen.

### 409 — l'agrégat a été modifié entre-temps

Deux commandes ont chargé le même agrégat, la première a écrit, la seconde
écrit sur une version périmée. Tout agrégat porte une Version (ADR 0020) que
le socle incrémente à chaque sauvegarde et compare à l'écriture : la seconde
commande est refusée entière — rien d'écrit, aucun event dispatché, aucune
ligne d'outbox — et le client recharge puis recommence.

```
POST /api/bank/accounts/{id}/deposits   {"amount":20,"currency":"EUR"}
→ 409
{"title":"Conflict","status":409,
 "code":"CONCURRENT_UPDATE",
 "parameters":{"id":"e9a613d4-cf97-4703-b2cc-6253ac90ff55"}}
```

`ConcurrentUpdateException` vit dans le SharedKernel — le code n'a pas de
préfixe — et c'est `ModuleDbContext` qui la lève, en traduisant l'échec de
concurrence d'EF avant le dispatch des events. Un module n'a rien à déclarer
pour en bénéficier, et rien à attraper : le filtre la sert comme toute
`DomainException`. Le front ne rejoue pas de lui-même — recharger l'état puis
recommencer est une décision de l'utilisateur, un rejeu automatique croiserait
l'idempotence des commandes (voir « Ce qui n'est pas là »).

### 400 — le corps de la requête est invalide

Le binding MVC échoue avant même d'atteindre le domaine. Le
`ValidationProblemDetails` par défaut d'`[ApiController]` est remplacé par la
même forme que les autres erreurs :

```
POST /api/bank/accounts/{id}/deposits   {"amount":"beaucoup","currency":"EUR"}
→ 400
{"title":"Bad Request","status":400,
 "code":"VALIDATION_FAILED","parameters":{"fields":["amount","command"]}}
```

```
POST /api/bank/accounts/{id}/deposits   (corps vide)
→ 400
{"title":"Bad Request","status":400,
 "code":"VALIDATION_FAILED","parameters":{"fields":["command"]}}
```

Le code est unique — `VALIDATION_FAILED`, sans préfixe de module, puisque
l'échec est arrivé avant qu'on sache de quel module il s'agissait. Les
paramètres ne portent que les champs fautifs, débarrassés du préfixe `$.` que
System.Text.Json ajoute à ses chemins JSON.

Ce qui a disparu au passage, et c'était le but : les messages de binding, qui
citaient `LoreBank.Bank.Application.Commands.DepositMoneyCommand` en toutes
lettres.

`command` dans `fields` est le nom du paramètre d'action qui n'a pas pu être
lié — pas un champ du corps. Il apparaît dès que la désérialisation du corps
échoue en entier. C'est un nom que nous choisissons, pas un type interne qui
fuit, mais le front doit savoir qu'il ne correspond à rien de visible dans sa
requête.

### 500 — une exception non métier remonte

`UnhandledExceptionHandler` est monté dans le pipeline (`app.UseExceptionHandler()`),
donc en amont de MVC : il attrape aussi ce qui échoue avant d'atteindre une
action.

```
POST /api/bank/accounts   (un handler de domain event lève InvalidOperationException)
→ 500, content-type application/problem+json; charset=utf-8
{"title":"Internal Server Error","status":500}
```

Deux décisions à connaître :

- **Pas de code, pas de paramètres.** Une exception non métier n'a rien de
  traduisible à offrir. Le front affiche un message générique.
- **Le même corps en dev et en prod.** La page d'exception de développement
  d'ASP.NET Core n'est pas montée : elle rendait le contrat HTTP dépendant de
  l'environnement, ce qui est exactement le piège qu'on veut éviter quand le
  front se fie à la forme des réponses. L'exception part dans `ILogger` — donc
  dans la console en dev, avec sa stack trace complète.

### Un handler de domain event échoue

Les domain events sont dispatchés depuis `SaveChangesAsync`, après l'écriture
mais **avant le commit** — donc à l'intérieur de la transaction de la commande.
Un handler qui échoue annule donc la commande entière.

C'est délibéré : un effet de bord métier qui rate ne doit pas laisser derrière
lui un fait métier enregistré.

Deux sous-cas, selon ce que lève le handler :

- il lève une `DomainException` → le filtre la traite comme n'importe quelle
  autre : **422** (ou 404) avec son code, **et rien n'est persisté** ;
- il lève autre chose → **500**, et rien n'est persisté non plus.

> Ce que les tests établissent : `TransactionRollbackTest` prouve qu'un handler
> qui lève annule bien l'écriture (le compte n'est pas persisté), et
> `ErrorContractTest` relève le 500 réel sur HTTP pour ce même cas. Le 422 d'un
> handler d'event, lui, reste déduit du filtre — qui ne regarde que le type de
> l'exception, pas sa provenance.

### Le plafond d'une minute est dépassé

Le `TransactionScope` du `TransactionBehavior` porte un `Timeout` explicite, égal
à `TransactionManager.DefaultTimeout`, soit une minute. Une commande dont les
handlers dépassent ce délai meurt en `TransactionAbortedException`, qui n'est pas
une `DomainException` : elle sort donc en **500**.

C'est la raison pour laquelle les handlers de domain events doivent rester
courts, et de préférence en processus. Un appel réseau lent tient en plus les
verrous des lignes écrites pendant toute sa durée.

> Déduit du code (`Timeout = TransactionManager.DefaultTimeout` dans
> `TransactionBehavior`), non provoqué.

### Récapitulatif

| Cas | Statut | `code` | Écriture annulée |
|---|---|---|---|
| Invariant du domaine violé | 422 | `<MODULE>.<VIOLATION>` | oui |
| Ressource introuvable (commande ou lecture) | 404 | `<MODULE>.<VIOLATION>` | oui |
| Version d'agrégat périmée | 409 | `CONCURRENT_UPDATE` | oui |
| Corps de requête invalide | 400 | `VALIDATION_FAILED` | — |
| Handler d'event : `DomainException` | 422 / 404 | `<MODULE>.<VIOLATION>` | oui |
| Handler d'event : autre exception | 500 | **aucun** | oui |
| Plafond d'une minute dépassé | 500 | **aucun** | oui |
| Exception non gérée | 500 | **aucun** | oui |

Une seule ligne sans code, et c'est toujours la même : le 500.

## Ce qui reste imparfait

Trois coutures, documentées plutôt que tues, parce qu'un développeur qui
construit sur ce socle finira par les toucher.

**`fields` mélange deux natures.** `["amount", "command"]` : le premier est un
champ du corps, le second le nom du paramètre d'action. Le front ne peut pas
distinguer les deux sans connaître la signature du controller. Aller plus loin
demanderait de filtrer les clés qui ne sont pas des chemins JSON — au prix de
perdre l'information « le corps entier est illisible ».

**Le filtre métier est un filtre MVC.** Une `DomainException` levée hors du
pipeline MVC — dans un middleware, dans un `IHostedService` — ne le rencontrerait
jamais et sortirait en 500 sans code. Ça ne se produit pas aujourd'hui, puisque
tout le domaine s'exécute sous une action de controller ; ça se produirait au
premier travail de fond.

**Aucune reprise sur erreur transitoire.** Une déconnexion passagère de
PostgreSQL devient un 500, sans nouvelle tentative. Voir ci-dessous — ce n'est
pas un oubli.

## Ce qui n'est pas là, et pourquoi

**Pas de reprise automatique sur erreur transitoire.** Le réflexe serait
d'activer `EnableRetryOnFailure()` sur Npgsql. **Ça ne marche pas ici**, et
l'échec est franc et immédiat :

```
System.InvalidOperationException : The configured execution strategy
'NpgsqlRetryingExecutionStrategy' does not support user-initiated transactions.
Use the execution strategy returned by 'DbContext.Database.CreateExecutionStrategy()'
to execute all the operations in the transaction as a retriable unit.
   at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.OnFirstExecution()
```

> Observé : `EnableRetryOnFailure()` ajouté à l'hôte de test fait échouer toute
> commande dès la première écriture.

La raison est structurelle : le `TransactionBehavior` ouvre un `TransactionScope`
ambiant, et l'`ExecutionStrategy` d'EF Core refuse de rejouer quoi que ce soit
tant que `Transaction.Current` n'est pas nul — elle ne peut pas garantir que sa
seconde tentative retomberait dans la même transaction.

Une reprise correcte devrait donc se placer **au-dessus** de la transaction, dans
le pipeline MediatR, et rejouer la requête entière — scope compris. C'est
faisable, mais ça ouvre une question que ce socle ne tranche pas à votre place :
un échec survenu **pendant** le commit est ambigu, la transaction ayant pu
aboutir sans que le client l'apprenne. Rejouer une commande dans ce cas peut la
dédoubler. Le remède n'est pas un compteur de tentatives mais une clé
d'idempotence portée par la commande — une décision qui vous appartient.

**Pas de 401 ni de 403.** Le socle n'authentifie ni n'autorise rien : c'est
un choix de fournisseur (Keycloak, Entra, Auth0…) que chaque cloneur ferait
à sa place — ADR 0023. Ce que le socle porte, c'est l'**Acteur** : qui agit,
identifié par l'identifiant opaque du fournisseur, Anonyme tant que personne
n'authentifie, fourni aux handlers par le port `ICurrentActor` et lu sur le
principal qu'ASP.NET Core expose de toute façon. Le jour où vous montez votre
schéma (`AddAuthentication` + `UseAuthentication`, une `FallbackPolicy` pour
fermer par défaut), les 401 et 403 qui apparaissent doivent rejoindre cette
forme — un `code` sans préfixe, pas de `detail` — et la liste des statuts de
la Description. Rien de tout cela n'existe aujourd'hui, volontairement.

## Côté front

### Le catalogue de traductions suit l'architecture

Le point du code sépare le module de la violation, ce qui donne un arbre :

```json
{
  "INVALID_IBAN": "« {iban} » n'est pas un IBAN valide.",
  "CURRENCY_MISMATCH": "Opération impossible entre {left} et {right}.",
  "VALIDATION_FAILED": "La requête est mal formée.",
  "BANK": {
    "INSUFFICIENT_BALANCE": "Solde insuffisant : {requested} demandés pour un solde de {balance}.",
    "ACCOUNT_CLOSED": "Ce compte est fermé.",
    "BANK_ACCOUNT_NOT_FOUND": "Ce compte est introuvable."
  }
}
```

Les codes du SharedKernel sont à la racine, ceux d'un module sous son nom. Le
front doit donc accepter les deux formes, avec et sans point.

### Interpoler, et formater soi-même

Les paramètres arrivent bruts. `{"balance": 0, "requested": 50, "currency": "EUR"}`
n'est pas un texte : c'est au front d'appliquer `Intl.NumberFormat` avec la
locale de l'utilisateur. C'est tout l'intérêt de ne pas recevoir `"0.00 EUR"`.

### Les codes sont typés

La Description OpenAPI (`backend/openapi/lorebank.json`, `docs/openapi.md`)
publie l'ensemble des codes sous un schéma `ErrorCode`, et `ApiProblem.code`
le référence. Le Client TypeScript en fait une union de chaînes : une table
de traduction typée `Record<ErrorCode, …>` (voir
`frontend/src/api/errorMessages.ts`) est complète ou ne compile pas, et
`openapi-fetch` type l'`error` de chaque appel par `ApiProblem`.

### Ce qu'il faut prévoir

1. **Un cas par défaut.** Un code inconnu du catalogue ne doit pas afficher le
   code brut à l'utilisateur — prévoir un message générique par statut. Avec
   la table typée, ce cas ne concerne plus que le front déployé avant le
   back qui a ajouté le code.
2. **Le 500 sans `code`.** C'est le seul cas ; tester la présence de `code`
   avant de chercher une traduction reste plus sûr que de compter dessus.
3. **Ne pas afficher `parameters.fields` tel quel.** C'est une liste de noms
   techniques, utile pour surligner un champ de formulaire, pas pour être lue.

## Ce que les tests garantissent

C'est `ErrorContractTest`
(`LoreBank.SharedKernel.Test.Infrastructure/Apis/`), et lui seul, qui épingle
ce document : statuts, type de média, présence du `code`, absence du `detail`,
et le fait qu'aucun nom de type .NET ni aucun message d'exception n'atteint le
client. Il vit dans le SharedKernel, comme la plomberie qu'il prouve — pas dans
un module métier qu'un cloneur du template supprimera — et se déclenche via le
`ProbeController`, un controller-sonde monté par `SharedKernelWebAppFactory`
seulement, avec une action par porte de sortie. (`CqsContractTest`, dans
`LoreBank.Bank.Test.Infrastructure/Apis/`, épingle le versant nominal — une
commande ne sert aucune représentation : c'est une discipline écrite dans
chaque controller, pas une plomberie du socle, donc sa preuve reste dans le
module de référence.)

Ces fixtures n'héritent pas de `BaseIntegrationTest` : le `TransactionScope`
ambiant de celui-ci ne traverse pas la frontière HTTP, et une requête servie
par l'hôte écrirait donc hors du rollback.
