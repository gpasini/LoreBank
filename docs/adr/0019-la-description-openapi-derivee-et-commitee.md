# La Description OpenAPI dérivée de ModuleController et commitée

> Statut : accepté — 2026-09-07.

L'hôte publiait un document OpenAPI (`AddOpenApi()` natif, Scalar, en
Development) qui mentait : toutes les opérations en `200` alors que
`ModuleController` sert 204 ou 201 + `Location`, aucune erreur documentée
alors que `docs/erreurs.md` fait du `code` l'identifiant que le front traduit,
`accountId` requis dans le body des routes mixtes alors que la route l'écrase,
`decimal` en `number | string`, pas d'`operationId`, `servers` au port du
poste. Un client TypeScript généré de là aurait obligé le front à envoyer un
champ ignoré et ne lui aurait rien dit des erreurs — la seule chose qu'il doit
traiter.

On décide que la **Description** (le terme de `CONTEXT.md` — « contrat »
reste aux Contrats de module) est dérivée du code, jamais déclarée à la main,
et commitée :

- `SendAsync` rend `Task<CommandResult>`, `CreateAsync` rend
  `Task<CreationResult>` — deux `ActionResult` du socle. Une convention MVC
  (`DescriptionConvention`, `LoreBank.SharedKernel.Api/OpenApi`) lit le type
  de retour de chaque action et en dérive 204 ou 201 + `Location` ; une
  action `ActionResult<T>` reste 200 + T. L'ADR 0011 avait fait du CQS du bord
  HTTP un fait du système de types ; la Description en devient une lecture,
  sans qu'un module écrive un `[ProducesResponseType]`. La convention pose
  aussi l'`operationId` `<Controller>_<Action>` et le tag au nom du module.
- Les transformers du socle complètent : 400, 404, 422, 500 sur **toutes**
  les opérations, uniformément, en `application/problem+json` sur un schéma
  `ApiProblem` unique (`code` et `parameters` optionnels — le 500 n'en a pas)
  ; un schéma `ErrorCode` énuméré par scan des `DomainException` concrètes
  (Domain et Application de chaque module monté, SharedKernel,
  `VALIDATION_FAILED`) ; `decimal` en `number` ; un seul media type par sens ;
  pas de `servers`, un titre.
- `[RouteBound]` (`LoreBank.SharedKernel.Application`) marque la propriété
  d'une commande que la route écrase ; elle sort du schéma du body. Le `with`
  du controller reste, l'ADR 0012 tient.
- `Microsoft.Extensions.ApiDescription.Server` émet
  `backend/openapi/lorebank.json` à chaque build de l'hôte ; la CI échoue si
  le fichier suivi diverge. Le renommage d'une propriété de commande ou de
  Result — invisible au compilateur, ADR 0012 — devient un diff de PR.
  `AddOpenApi` est inconditionnel (aucune surface réseau, l'émission compose
  l'hôte hors Development) ; l'endpoint et Scalar restent Development-only.
- Le front (`frontend/`, Vite + React + TS) génère ses types depuis le
  fichier commité avec `openapi-typescript` — types seuls, fichier ignoré par
  git — et appelle via `openapi-fetch`, qui type `error` par les réponses
  non-2xx. Un job CI installe, génère et lance `tsc` : une table de
  traduction typée `Record<ErrorCode, …>` est complète ou ne compile pas.

Preuves : `DescriptionContractTest` (sur le `ProbeController`, ADR 0017 —
le socle se prouve sur son terrain), `ErrorCodesDescriptionTest` (l'enum
égale le scan de `HostModules.All`), `ErrorCodesTest` (le scan lui-même),
et le fichier commité, relu en PR.

## Options écartées

- **`[ProducesResponseType]` sur chaque action** : recopiable par module,
  oubliable en silence — le geste que le socle refuse partout ailleurs.
- **Un transformer seul, sans typer les retours de `ModuleController`** :
  devinerait depuis le verbe HTTP ; la convention lit ce que le type affirme.
- **Le front lit `/openapi/v1.json` sur un serveur vivant** : la génération
  exige un back qui tourne, et la dérive n'apparaît jamais dans une PR.
- **Un document par module** : un déployable, un client ; `ApiProblem` et
  `ErrorCode` seraient dupliqués.
- **Les codes d'erreur par opération** : non dérivable statiquement.
- **Assumer `accountId` dans le body** (deux sources, l'une gagnant en
  silence) ou **un transformer par matching de noms** (`{id}` ↔
  `AccountId`, magique) : le marqueur explicite est le geste minimal.
- **`decimal` en `string`** : juste au-delà de 2^53 centimes ou pour de
  l'arithmétique front ; rupture du contrat wire, à décider quand le besoin
  existe.
- **orval / hey-api** : un client et des hooks générés ; le Client est des
  types, une couche data se posera dessus sans régénération.
- **Commiter aussi le TS généré** : deux artefacts pour une source.

## Le coût assumé

`[RouteBound]` est une annotation de bord HTTP dans l'Application — assumé,
l'ADR 0012 a fait de l'Application la surface HTTP. `CommandResult` et
`CreationResult` sont la signature de toute action de commande. Le nom d'une
action est du contrat wire (`operationId`). Chaque build de l'hôte compose
l'application (sans base) pour émettre le document. `openapi-typescript`
exige TypeScript 5.x (il consomme l'API compilateur que la 7 n'expose plus)
— pinné dans `frontend/package.json`.
