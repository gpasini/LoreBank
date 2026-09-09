# La Description OpenAPI, de l'action au type

Comment la surface HTTP d'un module est décrite depuis le code, commitée, et
devient le Client TypeScript du front — sans qu'un module déclare quoi que ce
soit. Décision et alternatives : ADR 0019.

## En une phrase

Le type de retour d'une action dit ce qu'elle sert ; le socle le lit, complète
le document avec ce qu'il sait (erreurs, codes), l'émet à chaque build dans
`backend/openapi/lorebank.json`, et le front en génère ses types.

## Ce que la convention lit

`DescriptionConvention` (`LoreBank.SharedKernel.Api/OpenApi`) passe sur chaque
action MVC :

| L'action rend | La Description dit |
|---|---|
| `Task<CommandResult>` (via `SendAsync`) | `204`, sans contenu |
| `Task<CreationResult>` (via `CreateAsync`) | `201`, en-tête `Location` requis, sans contenu |
| `ActionResult<T>` | `200` + le schéma de `T` (l'ApiExplorer le sait déjà) |
| `SignalStreamResult` (le flux de Signaux du socle, ADR 0026) | `200` en `text/event-stream`, schéma `Signal` |
| autre chose | ce que l'ApiExplorer infère |

Elle pose aussi l'`operationId` `<Controller>_<Action>` (`BankAccounts_GetById`)
— le nom d'action est déjà du contrat, `CreatedAtAction` le cible — et le tag
au nom du module, lu au 2ᵉ segment de l'assembly du controller comme le sont
les codes d'erreur. Métadonnées seulement : `[Produces]`/`[Consumes]` sont des
filtres qui réécriraient le type de média des réponses d'erreur.

## Ce que les transformers complètent

`DescriptionDocumentTransformer` et `DescriptionSchemaTransformer`, même
dossier :

- **Les erreurs, sur toutes les opérations** : `400`, `404`, `409`, `422`, `500` en
  `application/problem+json`, sur le schéma `ApiProblem` — `title`, `status`,
  `traceId`, `code?`, `parameters?` (le 500 n'a pas les deux derniers). Uniformément :
  la Description ne devine pas ce qu'un handler lève, et un `POST` de
  création peut très bien faire 404 sur une référence.
- **`ErrorCode`** : un `string` énuméré, trié — chaque `DomainException`
  concrète du Domain *et* de l'Application de chaque module monté (les
  `NotFoundException` vivent dans l'Application), celles du SharedKernel,
  et `VALIDATION_FAILED`. `ApiProblem.code` le référence.
- **`[RouteBound]`** : la propriété d'une commande que la route écrase sort
  du schéma du body — et de ses `required`.
- **`decimal`** → `number`, sans le pattern ni le `string` que le générateur
  ajoute parce que System.Text.Json accepte les deux en entrée.
- **Un media type par sens** : `application/json` en requête et en réponse
  nominale — `text/event-stream` sur la seule opération du flux de Signaux
  (`docs/signaux.md`).
- **Pas de `servers`, `title` = `LoreBank`** : le document décrit une
  surface, pas un déploiement — l'URL de base est au front.

## L'émission et la CI

`Microsoft.Extensions.ApiDescription.Server`, référencé par l'hôte, compose
l'application après chaque build (`HostFactoryResolver` — pas de HTTP, pas de
base, pas de hosted service) et écrit `backend/openapi/lorebank.json`
(`OpenApiDocumentsDirectory` dans `LoreBank.Host.csproj`). Le fichier est
commité : un renommage de propriété de commande ou de Result, que le
compilateur ne voit pas (ADR 0012), apparaît dans le diff de la PR.

Le step « Description OpenAPI à jour » de la CI échoue si le fichier suivi
diverge après le build. En local, rien à faire : le build l'a déjà réécrit,
il ne reste qu'à le commiter avec le changement qui l'a produit.

`AddOpenApiDescription` (le geste unique de l'hôte) enregistre `AddOpenApi`
inconditionnellement — l'émission compose l'hôte hors Development. L'endpoint
`/openapi/v1.json` et Scalar restent Development-only : la prod ne publie pas
sa propre description.

## Le Client

`frontend/` génère `src/api/schema.d.ts` depuis le fichier commité
(`npm run generate`, appelé par `prepare` à l'install) avec
`openapi-typescript` — des types, rien d'autre, jamais commités ni édités.
`src/api/client.ts` crée le client `openapi-fetch` typé par ces types :
chaque appel rend `{ data, error }`, `data` typé par la réponse nominale,
`error` par les réponses d'erreur — donc `ApiProblem`, donc `error.code` est
un `ErrorCode`.

`src/api/errorMessages.ts` est typé `Record<ErrorCode, …>` : un code que le
back ajoute et que la table ne traduit pas est une erreur de compilation. Le
job `frontend` de la CI (install, generate, typecheck) est ce qui rend cette
garantie effective.

## Sur une route mixte

```csharp
public sealed record DepositMoneyCommand(
    [property: RouteBound] Guid AccountId,
    decimal Amount,
    string Currency
) : ICommand;
```

Le controller écrit toujours `command with { AccountId = id }` ; le Client
n'envoie que `amount` et `currency`. Oublier le marqueur ne casse rien à
l'exécution — c'est le diff du document commité qui montre un `accountId`
apparu dans un body.

## Ce que les tests garantissent

- `DescriptionContractTest` (`LoreBank.SharedKernel.Test.Infrastructure/Apis/`)
  : sur le `ProbeController` — 204 et 201 + `Location` sans contenu, 200 +
  Result, les quatre erreurs sur `ApiProblem` partout, `operationId`, tag,
  `application/json` seul, `decimal` en `number`, la propriété `[RouteBound]`
  absente, `ApiProblem.code` → `ErrorCode`, pas de `servers`.
- `ErrorCodesDescriptionTest` (`Hosting/`) : l'enum servie égale le scan de
  `HostModules.All` — un module monté dont les codes manqueraient rendrait le
  Client incomplet en silence.
- `ErrorCodesTest` (`LoreBank.SharedKernel.Test.Unit/OpenApi/`) : le scan —
  concrètes seulement, SharedKernel et `VALIDATION_FAILED` toujours, trié.
- Le fichier commité lui-même, relu en PR, et le job `frontend`.
