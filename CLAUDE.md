# LoreBank

Monolithe modulaire .NET en Clean Architecture / DDD. Ce repo est une base de
départ : clone-le, renomme, et construis tes modules métier sur ce socle.
Le module `Bank` sert d'exemple de référence pour construire un module ;
`Ledger` (la comptabilité, nourrie par les events de `Bank`) pour la
communication inter-modules.

## Build & toolchain

- Le SDK .NET vient de mise : `mise exec -- dotnet <cmd>`, jamais le dotnet du PATH.
- C'est `backend/global.json` qui sélectionne la version du SDK (le pin de
  `backend/mise.toml` installe l'outil mais ne suffit pas à le sélectionner).
- Build : `cd backend && mise exec -- dotnet build LoreBank.slnx`.
- Les propriétés MSBuild communes (`TargetFramework`, `Nullable`,
  `ImplicitUsings`, `TreatWarningsAsErrors` — la doctrine « build sans
  warning » est tenue par le compilateur — `AnalysisMode=Recommended`,
  `EnforceCodeStyleInBuild`, l'audit NuGet épinglé) vivent dans
  `backend/Directory.Build.props` — ne pas les dupliquer dans les csproj.
- Les **portes de qualité** (ADR 0028, `docs/qualite.md`) sont des tâches
  mise, une par porte, et la CI (`.github/workflows/ci.yml`, sur push vers
  `master` et sur PR, SDK installé par mise comme sur le poste) a un step
  par tâche : build, `format:check` (`dotnet format --verify-no-changes`),
  `openapi:check`, `test` côté back ; `typecheck`, `check` (Biome) et
  `audit` (`npm audit --audit-level=high`) côté front. `mise run check` à
  la racine rejoue tout ; `mise run pre-commit` les rapides, installable en
  hook par `mise generate git-pre-commit --write`. Les exemptions
  d'analyseurs vivent dans `.editorconfig` avec leur pourquoi, et sont
  **gelées** (ADR 0031, `docs/qualite.md`) : `QualityGateFreezeTest` épingle
  la liste des desserrages admis — exemptions, exclusions de couverture,
  propriétés de build, symboles bannis, liste des portes — et interdit ce
  qui la contourne (un test ignoré, un `SuppressMessage`, un `NoWarn`, un
  `#pragma` hors code généré). Desserrer reste légitime ; le geste est
  explicite. La
  **couverture** (ADR 0029) est une mesure hors des portes, jamais
  seuillée : `mise run //backend:coverage` rend `backend/coverage/report/`
  (`coverlet.collector` déclaré une fois dans `Directory.Build.props`,
  exclusions dans `coverage.runsettings`, ReportGenerator dans le manifest
  d'outils), et la CI en met le résumé par assembly dans la page du run.
- Les versions de packages sont centralisées dans
  `backend/Directory.Packages.props` (central package management) : un csproj
  référence sans attribut `Version`, et les pins — licence (FluentAssertions
  7.x, MediatR 12.x), avis de sécurité — y sont des faits uniques, commentés à
  côté de la version.
- Le front vit dans `frontend/` (Vite + React + TypeScript), Node pinné par
  mise (`frontend/mise.toml`, racine monorepo déclarée dans `mise.toml`) :
  `cd frontend && mise exec -- npm ci` génère le Client depuis
  `backend/openapi/lorebank.json` (`prepare`) ; `npm run typecheck`,
  `npm run check` (Biome : lint et format, `npm run format` corrige) et
  `npm run audit` sont ses portes. TypeScript est pinné en 5.x —
  `openapi-typescript` consomme l'API compilateur que la 7 n'expose plus.

## Architecture

- Un module = 6 projets `LoreBank.<Module>.{Domain, Application, Infrastructure,
  Api, Test.Unit, Test.Infrastructure}`, à plat dans `backend/`, regroupés dans
  la solution sous le dossier `Modules/<Module>` — +1 `Contracts` si le module
  publie (ADR 0015) : son langage publié — integration events et ports de
  lecture publics — la **seule** surface qu'un autre module a le droit de
  référencer, et dont la seule dépendance est `SharedKernel.Contracts` (aucun
  VO possible : primitives seulement, matériellement).
- Les projets `Api` des modules sont des classlibs de controllers MVC (pas de
  minimal API). L'hôte unique `LoreBank.Host` (dossier de solution `Host`)
  porte la composition (DI, filtres), sa Vivacité et sa Disponibilité
  (`/health/live`, `/health/ready` — un check du socle par base de module,
  hors Description), ses logs en console JSON avec la Corrélation, et le
  `Meter` des outbox (`lorebank.outbox.pending`/`poisoned` par module, sans
  exporteur — ADR 0022), sa Télémétrie (`Telemetry.cs`, le seul endroit qui
  référence OpenTelemetry : traces HTTP, Npgsql et livraisons d'outbox —
  `OutboxTracing`, une activité par handler, enfant de la commande d'origine
  — et métriques, exportées en OTLP seulement si
  `OTEL_EXPORTER_OTLP_ENDPOINT` est défini — ADR 0025, `docs/telemetrie.md`),
  et monte les modules à travers le seam
  `IHostModule` de `LoreBank.SharedKernel.Infrastructure` : chaque module a un
  adapter dans `LoreBank.Host/Modules/` dérivant de `HostModule<TDbContext>`
  (voir `BankModule`), qui ne déclare que son `Module` Autofac. Le reste de
  l'identité est dérivé du `TDbContext` (au singulier — un DbContext par
  module ; `ModuleMigrator` migre via lui, et la contrainte générique impose
  `ModuleDbContext`) : la base lit `<Racine>.<Module>` sur le nom de son
  assembly — trois segments exactement, échec bruyant au premier contact avec
  `HostModules.All` sinon — charge les assemblies de controllers,
  d'application et de domaine (`DomainAssembly`, que l'hôte scanne à la
  recherche des handlers de domain events), expose `ModuleName` (ADR 0007) et
  monte la persistance par défaut : clé `<Module>Db` sous `ConnectionStrings`,
  validée par `AddModuleDbContext` — clé absente ou `Enlist=false` cassent à
  la composition — et surchargeable via `ConfigureDbContext`, qui reste sur le
  seam (ADR 0008). La
  liste `HostModules.All` est la seule source de vérité : `Program.cs` la
  boucle, et `ModuleCompositionTest`
  (`LoreBank.SharedKernel.Test.Infrastructure`) itère la même pour vérifier que
  chaque requête MediatR résout son handler, que chaque controller est monté,
  que chaque handler de domain event se résout, qu'aucun
  `IDomainEventHandler<>` ne vit hors de la `DomainAssembly` (rangés dans
  `Application/`, ils échapperaient au scan en silence), que le namespace de
  chaque `DomainException` du module nomme le module en 2ᵉ segment (c'est lui
  qui préfixe les codes d'erreur), qu'aucune migration ne manque, et que les
  migrations de données sont bien formées — `[DataMigration]` à 14 chiffres,
  ids uniques, rangées dans l'assembly du DbContext, toutes journalisées après
  la migration du harnais (ADR 0013). Ses voisins `DomainConventionTest` et
  `ApplicationConventionTest` épinglent de la même façon les conventions du
  domaine (constructeurs d'agrégats privés, VO immuables, `sealed` sur les
  familles fermées) et de l'Application (une query sans repository d'agrégat).
  Ajouter un module = écrire son adapter et
  l'ajouter à `HostModules.All` — rien d'autre côté hôte (décision et
  alternatives écartées : `docs/adr/0001-montage-de-module-via-ihostmodule.md`),
  procédure : skill `nouveau-module`.
- Les blocs de base partagés vivent dans `LoreBank.SharedKernel.Domain` (dossier
  de solution `SharedKernel`) : `Entity`, `AggregateRoot`, `ValueObject`,
  `SimpleValueObject`, `IDomainEvent`, `IDomainEventHandler`, `DomainException`,
  `NotFoundException`, et les VO transverses (`Iban`, `Bic`, `Money`,
  `PositiveMoney` — le montant d'une opération, strictement positif : une
  transition qui prend un `Money` nu accepterait un montant négatif qui
  inverse son sens — et `Actor`, l'Acteur : qui agit, identifiant opaque du
  fournisseur d'identité ou Anonyme, reçu en paramètre par une transition
  qui enregistre son auteur, jamais demandé par le Domain — ADR 0023). Même
  règle pour l'Instant (ADR 0024) : une transition qui date un fait reçoit un
  `DateTimeOffset` en paramètre, le Domain ne lit jamais l'horloge, et un
  handler de domain event reprend l'Instant que l'event porte. Pas de VO ni
  de port `IClock` : l'analyseur d'API bannies (`backend/BannedSymbols.txt`,
  activé par `Directory.Build.props` sur tout projet `.Domain` ou
  `.Application`, socle inclus) fait échouer le build en `RS0030` sur
  `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now` et
  `DateTimeOffset.UtcNow`. Le même
  dossier porte `LoreBank.SharedKernel.Api`, qui accueille ce que tous les
  modules partagent côté HTTP : `Controllers/ModuleController` (la base des
  controllers de module — ADR 0011), `Problems/ApiProblem` (la forme unique d'une
  réponse d'erreur), `Filters/DomainExceptionFilter` (les erreurs métier),
  `Validation/ValidationProblemFactory` (les 400 de binding) et
  `Handlers/UnhandledExceptionHandler` (tout le reste, en 500).
  `LoreBank.SharedKernel.Infrastructure` complète la paire côté plomberie : ce
  que tous les modules partagent en implémentation — le `DomainEventDispatcher`,
  la base `ModuleDbContext`, le seam de montage `IHostModule` et la machinerie
  des integration events (`IntegrationEvents/` : les stores `Outbox` et
  `Inbox` — chacun porte le DDL et les colonnes de sa table, écrits une
  seule fois —, l'accès `IntegrationEventStores` qui nomme les deux
  politiques de scope, le publisher et le dispatcher).
  `LoreBank.SharedKernel.Contracts` (projet volontairement sans aucune
  référence) porte les marqueurs du langage publié : `IIntegrationEvent`,
  `[IntegrationEvent]`, `IIntegrationEventHandler<T>` et le port
  `IIntegrationEventPublisher`.
- La communication inter-modules a deux canaux, jamais une commande qui
  traverse (le scope ambiant escaladerait en distribué). **Asynchrone** :
  un domain event handler du module publieur mappe le fait interne vers son
  jumeau plat suffixé `IntegrationEvent` (types distincts, primitives — ADR
  0015) et le confie à `IIntegrationEventPublisher` — la ligne d'outbox part
  dans la transaction de la commande ou pas du tout ; la publication est
  opt-in, seuls les faits mappés sortent. Un hosted service unique dépile
  toutes les outbox ; chaque handler consommateur (`IIntegrationEventHandler`,
  découvert par scan Domain+Application) tourne dans son propre
  `TransactionScope`, ligne d'inbox incluse — at-least-once, rejeu inoffensif,
  backoff puis poison (ADR 0014). Le discriminant (`bank.money-deposited`,
  premier segment = module publieur) est un nom stable choisi, jamais un nom
  de type .NET. Les tables `__outbox`/`__inbox` naissent via `ModuleMigrator`,
  qui pose le schéma puis demande à chaque store son DDL (idempotent, sans
  timeline de migration) ; plusieurs instances de
  l'hôte se partagent chaque outbox par Réservation à bail, et les lignes
  livrées ou traitées sont purgées après la Rétention — trois réglages
  d'`OutboxOptions`, poison jamais purgé (ADR 0021). Aucun de ces gestes
  n'écrit son SQL sur place : la forme d'une ligne vit dans son store, le
  processor ne garde que l'orchestration et la politique (backoff, seuil de
  poison, jauges), et le harnais observe par le même store au lieu de
  réécrire les colonnes.
  **Synchrone** : un port de lecture publié dans les Contrats
  (`IBankAccountsContract`), implémenté chez le propriétaire comme un reader —
  DTOs plats, `null` pour l'absence, lecture pure in-process. Garde-fous :
  `OutboxPublisherTest`, `OutboxProcessorTest` (socle) et
  `IntegrationEventPublicationTest` (le module de référence emprunte vraiment
  le chemin) — ce dernier écrit sur `OutboxProbe`, la surface d'observation
  d'outbox du harnais (`ReadRowsAsync<TDbContext>`/`CleanAsync<TDbContext>`) :
  le test de publication d'un module se réduit à agir en HTTP puis affirmer
  discriminant, payload et ressource de Signal. Le chemin complet entre deux modules d'exemple —
  dépôt HTTP chez Bank, passe du processor, écriture chez Ledger, lecture
  HTTP de la Liste des mouvements du Ledger — est joué par le `CqsContractTest` du Ledger ; la ligne
  d'inbox n'y est pas affirmée, c'est un invariant du socle prouvé sur Probe
  (ADR 0017).
- Le **Signal** (ADR 0026, `docs/signaux.md`) est le troisième consommateur
  des faits, côté clients : un message nu — discriminant, ressource
  (`resourceKind` stable en kebab-case + `resourceId`), Instant du fait,
  jamais d'état — poussé en SSE sur `GET /api/signals` (`SignalsController`
  et `SignalStreamResult` dans `LoreBank.SharedKernel.Api`, la seule route
  du socle côté clients, dans la Description en `text/event-stream` sur le
  schéma `Signal`) quand un integration event a été **livré** : la ligne
  d'outbox marquée livrée est la notification — après tous ses handlers,
  donc après la ligne du Ledger. Opt-in par l'event, qui implémente
  `ISignalsClients` (`SharedKernel.Contracts`) et nomme sa ressource ; le
  publisher l'écrit dans deux colonnes de la ligne (`resource_kind`,
  `resource_id`), hors payload. Chaque instance de l'hôte suit ses outbox
  (`SignalTailer`, dans la boucle de l'`OutboxDispatcher` après la
  livraison, un curseur par module né au premier passage — le harnais
  l'amorce avant d'agir) et pousse au `SignalHub` (port `ISignalStream` de
  `SharedKernel.Application`), canal borné par abonné, débordement =
  déconnexion, le navigateur reconnecte et relit. Filtre
  `?resource=kind/guid` répétable (mal formé : 422
  `INVALID_SIGNAL_RESOURCE`), ni `event:` ni `id:` ni rejeu, keep-alive en
  commentaire (`Signals:KeepAliveSeconds`). Le socle n'autorise rien : le
  port `ISignalPolicy` (Acteur + Signal → bool, défaut « tout passe ») est
  l'endroit où le cloneur décide, comme `ICurrentActor`. Meter
  `LoreBank.Signals` (abonnés, livrés), pas de trace par Signal. Le front
  ouvre un `EventSource` par onglet (`SignalsProvider`, hook `useSignals`)
  et relit son `GET` ; sa relecture immédiate après sa propre commande
  reste. Garde-fous : `SignalHubTest`, `SignalStreamResultTest` (unitaire),
  `SignalTailerTest`, `SignalContractTest` (socle, sur Probe),
  `ModuleCompositionTest` (un `ISignalsClients` sans `[IntegrationEvent]`),
  `SignalPublicationTest` (Bank, sur `SignalProbe` : ouvrir le flux, agir,
  passe du processor puis du suiveur, lire le Signal).

## Conventions du domaine

- Les erreurs métier sont des exceptions : une classe par violation, héritant de
  `DomainException`, `sealed`. Pas de `Result`. Une exception ne porte aucun
  texte : elle passe à sa base un dictionnaire de paramètres nommés, et son code
  — l'identifiant que le front internationalise — est dérivé de son type, de la
  forme `<MODULE>.<VIOLATION>` (`LoreBank.Bank.Domain.Exceptions.
  InsufficientBalanceException` → `BANK.INSUFFICIENT_BALANCE`). Les exceptions du
  SharedKernel n'ont pas de préfixe (`INVALID_IBAN`). La dérivation lit le 2ᵉ
  segment du namespace : elle suppose le nommage `<Racine>.<Module>.<Couche>`.
- Les valeurs des paramètres sont des primitives, jamais des value objects : on
  passe `balance.Amount` et `balance.Currency`, pas `balance`. Sinon la forme
  interne des VO devient un contrat public, et le front reçoit une valeur déjà
  formatée qu'il ne peut plus adapter à la locale de l'utilisateur. Les clés du
  dictionnaire sont en camelCase (`accountId`, pas `AccountId`) : ASP.NET Core
  ne les convertit pas — le `DictionaryKeyPolicy` est nul, elles partent
  telles quelles sur le fil.
- `Exception.Message` est fabriqué automatiquement à partir du code et des
  paramètres, en culture invariante : il sert aux logs, jamais au client.
  Les exceptions « introuvable » héritent de `NotFoundException`.
- Les value objects sont des classes, pas des records — choix délibéré :
  l'égalité vient de `ValueObject`. Immuables, et **validés à la création** —
  aucune instance invalide ne peut être créée : constructeur privé, une
  factory nommée qui normalise puis valide (`Iban.Parse`, `Money.Of`,
  `XxxId.New`), et `Hydrate` — non validant, réservé aux conversions EF —
  pour la réhydratation, qui truste la base (ADR 0016 : la validation vit aux
  frontières, ce sont les data migrations qui maintiennent le stock ; jamais
  de `Hydrate` depuis du code métier pour un VO porteur de règles — un id
  `SimpleValueObject<Guid>` sans invariant réhydrate aussi l'id reçu du fil,
  voir `BankAccountId` — ni de `Hydrate` sans appelant).
  `DomainConventionTest` interdit tout constructeur public sur un VO.
  Procédure et pièges : skill `nouveau-value-object`.
- Un agrégat naît par sa factory statique (constructeur privé), garde ses
  invariants dans ses méthodes de transition — les VO portent les leurs, on ne
  revérifie pas ce qu'un VO garantit — et émet un domain event par transition
  (`record sealed`, nommé au passé et suffixé `DomainEvent`). Procédure :
  skills `nouvel-agregat` et `nouveau-domain-event-handler`.
- Les handlers de domain events vivent dans le Domain (`EventHandlers/`) ; leurs
  dépendances sont des ports — interfaces dans `Services/`, implémentées par
  l'Infrastructure. Ils sont dispatchés par le `SaveChangesAsync` de `ModuleDbContext`
  (la base de tout `DbContext` de module), qui ramasse les events des entités
  trackées via `IHasDomainEvents`, les vide, écrit, puis les remet à
  `IDomainEventDispatcher`. Le dispatch a donc
  lieu **dans la transaction de la commande** : un handler qui échoue l'annule
  entièrement. C'est délibéré — un effet de bord métier qui rate ne doit pas
  laisser derrière lui un fait métier enregistré. Les handlers s'enregistrent
  côté hôte : `Program.cs` scanne la `DomainAssembly` de chaque `IHostModule` —
  rien à câbler dans le module.
- Faire tourner les handlers dans la transaction de la commande a un coût :
  les lignes qu'ils touchent restent verrouillées le temps de leur exécution,
  donc un handler doit rester court et de préférence in-process — la commande
  a un plafond implicite d'une minute. La garantie ne joue que dans un sens :
  un handler qui échoue annule la commande, mais un handler qui réussit avant
  un commit qui échoue laisse son effet de bord fait, sans rien derrière pour
  le rattraper. Un effet de bord externe irréversible veut une outbox, pas ce
  mécanisme.
- Les repositories d'agrégats sont des ports du Domain (`Repositories/`),
  implémentés par l'Infrastructure. Le port expose `GetRequiredByIdAsync`
  (non nullable — l'absence lève la `NotFoundException` du module, le handler
  n'a pas de `?? throw` à écrire) et `SaveAsync` ; pas de variante nullable,
  une sonde d'existence n'est pas un usage. Côté Api, les erreurs métier
  (`DomainException`) deviennent des ProblemDetails via le `DomainExceptionFilter`
  de `LoreBank.SharedKernel.Api`, enregistré une fois par l'hôte : 422, ou 404
  pour une `NotFoundException`. La réponse porte `code`, `parameters` et
  `traceId` (la Corrélation, ADR 0022) en extensions, et aucun `detail` — le
  back ne produit pas de texte destiné à l'utilisateur. Exemple, pour un
  solde insuffisant :

  ```json
  {
    "title": "Unprocessable Entity",
    "status": 422,
    "code": "BANK.INSUFFICIENT_BALANCE",
    "parameters": { "balance": 10.00, "requested": 20.00, "currency": "EUR" }
  }
  ```

  Toutes les réponses d'erreur ont cette forme, y compris celles que le domaine
  n'a jamais vues : le 400 de binding (`ValidationProblemFactory` remplace le
  `ValidationProblemDetails` d'`[ApiController]`, dont les messages citent des
  types .NET — code unique `VALIDATION_FAILED`, paramètre `fields`) et le 500
  (`UnhandledExceptionHandler`, monté via `app.UseExceptionHandler()` donc en
  amont de MVC — sans `code`, et identique en dev et en prod : la page
  d'exception de développement n'est volontairement pas montée, l'exception part
  dans `ILogger`). `ApiProblem` centralise la forme pour que ces trois portes de
  sortie ne divergent pas. Le type de média est
  `application/problem+json; charset=utf-8` partout. Voir `docs/erreurs.md`.

## Couche Application

- CQS avec MediatR (pinné en 12.x, dernière version sous licence Apache 2.0),
  derrière les marqueurs de `LoreBank.SharedKernel.Application` : `ICommand`
  (mute, ne retourne rien), `ICreationCommand` (seul retour admis : le `Guid`
  créé), `IQuery<TResponse>` (lit, retourne son Result immuable — jamais
  l'agrégat). Une query rend un `TResponse` **non nullable** : l'absence de la
  ressource est une erreur métier, pas une valeur de retour, et son handler lève
  la `NotFoundException` du module. C'est ce qui garantit qu'un 404 porte
  toujours un `code`, qu'on soit passé par une commande ou par une lecture — le
  controller n'a donc aucun cas d'absence à traiter, et jamais de `NotFound()` à
  écrire. Corollaire : une query ne peut pas servir de sonde d'existence.
  Le socle n'authentifie ni n'autorise rien — un choix de fournisseur laissé
  au cloneur (ADR 0023) — mais fournit l'Acteur par le port `ICurrentActor`
  de `LoreBank.SharedKernel.Application`, que le handler d'une commande prend
  en dépendance pour le passer à la transition ; l'implémentation du socle
  (`HttpContextActor`, `LoreBank.SharedKernel.Api`, enregistrée par l'hôte)
  lit le principal exposé par ASP.NET Core — Anonyme sans schéma monté,
  l'identifiant du principal dès que le cloneur monte le sien, sans rien
  changer aux modules ; en base et dans un Result, l'Anonyme est `null`, et
  le harnais d'un module agit « en tant que » par un fake du port
  (`ConfigurableCurrentActor`, fourni par le harnais du socle à tout hôte de
  test sauf au sien — le seul qui garde `HttpContextActor`, parce qu'il le
  prouve ; `ActorCompositionTest` tient cet opt-out, que le contrat seul ne
  verrait pas, le fake rendant Anonyme par défaut comme le vrai). L'Instant suit le même chemin
  (ADR 0024) : le handler d'une commande ou d'un integration event prend
  `TimeProvider` — le `TimeProvider.System` de la BCL, enregistré par
  l'hôte, sans port du socle par-dessus — et passe `GetUtcNow()` à la
  transition (`OpenedAt` chez Bank, `RecordedAt` chez Ledger, l'Instant de
  comptabilisation) ; le harnais le remplace par `ConfigurableTimeProvider`
  (`LoreBank.SharedKernel.Test.Infrastructure`, exposé par la factory de
  base, effacé par `ResetFakes`) : un test pose l'Instant avant son arrange
  et relit exactement cette valeur.
- `ICommand` et `ICreationCommand` portent le marqueur `IMutatingRequest`, que
  `IQuery<TResponse>` n'a pas. C'est lui, et lui seul, qui décide de ce que le
  `TransactionBehavior` de `LoreBank.SharedKernel.Application` enveloppe : toute
  commande s'exécute dans un `TransactionScope` ambiant en `ReadCommitted`, les
  lectures non. Le behavior ne connaît aucun `DbContext` — les connexions
  ouvertes à l'intérieur du scope s'y enrôlent d'elles-mêmes, ce qui le rend
  indépendant du nombre de modules. Le scope ambiant n'est pas un garde-fou de
  frontière : selon que les connexions des deux modules sont ouvertes
  simultanément ou non, Npgsql peut réutiliser le même connecteur (et la
  commande passera silencieusement) ou en enrôler un second (et la transaction
  escaladera en distribué, non supporté hors Windows). Ne comptez pas dessus
  pour interdire une commande qui traverse deux modules — c'est une règle
  d'architecture à tenir, pas une contrainte technique.
- `ReadCommitted` ne protège pas un lire-modifier-écrire : deux dépôts
  concurrents peuvent lire le même solde. Le remède est la **Version
  d'agrégat** (ADR 0020), pas un niveau d'isolation plus strict :
  `ModuleDbContext` pose par convention une propriété shadow `version`, jeton
  de concurrence, sur tout `AggregateRoot`, l'incrémente à chaque sauvegarde
  qui touche l'agrégat (racine ou VO owned) et traduit l'échec EF en
  `ConcurrentUpdateException` (409 `CONCURRENT_UPDATE`) avant le dispatch —
  un module n'a rien à déclarer, `ModuleDbContextTest` et
  `ModuleRepositoryTest` épinglent la convention. `Enlist=false` dans la
  chaîne de connexion est refusé dès la composition par `AddModuleDbContext`
  (ADR 0008) : sans ce
  garde-fou, les commandes cesseraient simplement d'être transactionnelles,
  sans erreur ni avertissement ni test qui échoue — Npgsql enrôle par défaut
  (`Enlist` vaut `true`). Un module qui surcharge `ConfigureDbContext` sans
  passer par l'extension reprend ce risque à sa charge.
- Une query ne passe **pas** par le repository de l'agrégat : elle dépend d'un
  port de lecture (`Readers/`, `IBankAccountReader`), déclaré dans l'Application
  parce qu'il rend un Result, et implémenté par l'Infrastructure. Un
  repository charge un agrégat pour le muter — value objects reconstruits, entité
  suivie par le change tracker ; une lecture n'a besoin que des colonnes qu'elle
  affiche. Le port rend `null` quand la ligne n'existe pas : pour un lecteur
  l'absence est un résultat normal, et c'est le handler de query qui la
  transforme en `NotFoundException`. Conséquence : `LoreBank.Bank.Infrastructure`
  référence `LoreBank.Bank.Application`, et un Result ne dépend plus
  du tout du modèle d'écriture.
- Une **Liste** (ADR 0027) a une seule forme : la query dérive de
  `ListQuery<TItem>` (`SharedKernel.Application` — `Page` ≥ 1, `PageSize`
  20 par défaut et 100 au plus, constantes du socle, `Search`), se lie
  `[FromQuery]` sur la query string, déclare ses filtres en propriétés
  typées **multi-valeurs** (`IReadOnlyList<string>? Currency` —
  `currency=EUR&currency=USD` : OU dans un filtre, ET entre filtres), et
  rend la **Page** du socle, `ListPage<TItem>` (`items`, `page`,
  `pageSize`, `totalCount`, `facets`) — l'item reste le Result propre à la
  query, l'enveloppe n'en est pas un. Le tri est celui du reader, jamais un
  paramètre ; une page au-delà de la dernière est une Page vide, pas un
  404 ; hors bornes, le moteur lève `InvalidPagingException` (422
  `INVALID_PAGING`), par HTTP comme par `ISender`. Le port de lecture
  reçoit la query entière. Une Liste **sous une ressource** (les mouvements
  d'un compte, `GET api/ledger/bank-accounts/{id}/movements`) porte une
  propriété `[RouteBound]` que le controller réécrit depuis la route — pas
  un filtre, décrite sur la route seulement — et son handler vérifie la
  ressource avant de lire (404 du module, jamais une Page vide).
  `ApplicationConventionTest` épingle la forme
  (une query qui rend une Page dérive de `ListQuery`, ses filtres sont des
  `IReadOnlyList<>`), `ListContractTest` la mécanique sur le Probe.
- Un dossier par use case dans `Commands/` ou `Queries/`, le Result d'une
  query colocalisé dans son dossier (ADR 0012 : un Result appartient à
  exactement une query) ; ce qui est partagé entre use cases reste dans un
  dossier transverse (`Readers/`, `Exceptions/`). Procédure complète — record,
  handler, action, tests, contrat : skills `nouvelle-commande` et
  `nouvelle-query`.
- Les controllers dérivent de `ModuleController` (`LoreBank.SharedKernel.Api`,
  ADR 0011), ne parlent qu'à `ISender`, et tiennent le CQS jusqu'au bord
  HTTP : **une action qui mute ne renvoie aucune représentation**, une action qui
  lit en renvoie une. Le geste est typé par la base : `SendAsync(ICommand)` →
  `204`, `CreateAsync(ICreationCommand, actionName)` → `201` + `Location` et un
  corps vide (le `Guid` d'`ICreationCommand` ne sert qu'à bâtir l'en-tête) —
  une query ne peut emprunter aucun des deux chemins, et `[ApiController]` est
  hérité de la base. Le contrat HTTP d'un module est sa surface Application
  (ADR 0012) — pas de dossier `Contracts/` : le body se lie directement sur
  la commande (sur une route mixte, le controller réécrit `command with
  { AccountId = id }` — la route est autoritaire, un champ posté en double
  est écrasé), et une lecture sert le Result de sa query tel quel.
  Renommer une propriété de commande ou de Result est donc un breaking change
  HTTP : le compilateur n'en dit rien, c'est `CqsContractTest` qui épingle
  l'ensemble exact des clés JSON du `GET` — et le diff de
  `backend/openapi/lorebank.json` qui le montre en PR. Un besoin de forme wire divergente
  se règle par une autre query avec son propre Result, jamais par un record
  de réponse dans l'Api. Le client qui veut l'état d'après fait un
  `GET`. C'est un aller-retour de plus, assumé : une commande qui renvoie aussi
  la ressource est également une lecture, et la représentation qu'elle sert peut
  diverger de celle du `GET` sans que rien ne le signale.
- La **Description OpenAPI** (ADR 0019, `docs/openapi.md`) est dérivée du
  code, jamais déclarée à la main : `SendAsync` rend `Task<CommandResult>`,
  `CreateAsync` rend `Task<CreationResult>`, et `DescriptionConvention`
  (`LoreBank.SharedKernel.Api/OpenApi`) lit ce type de retour pour dire 204
  ou 201 + `Location` — aucun `[ProducesResponseType]` dans un module. Les
  transformers du socle ajoutent 400/404/422/500 sur toutes les opérations,
  sur le schéma `ApiProblem` dont `code` est l'enum `ErrorCode` (scan des
  `DomainException` concrètes du Domain et de l'Application de chaque module
  monté + SharedKernel + `VALIDATION_FAILED`), l'`operationId`
  `<Controller>_<Action>`, le tag au nom du module, `decimal` en `number`
  et `int` en `integer`, les paramètres de query string en camelCase (une
  `ListQuery` liée `[FromQuery]` est décrite propriété par propriété — le
  binding reste insensible à la casse, et le 400 de binding nomme ses
  champs de la même façon), `application/json` seul, pas de `servers`. Sur une route mixte, la
  propriété que la route écrase est `[property: RouteBound]`
  (`LoreBank.SharedKernel.Application`) — elle sort du schéma du body, le
  `with` du controller reste. L'hôte monte tout par `AddOpenApiDescription`,
  inconditionnel ; `/openapi/v1.json` et Scalar restent Development-only.
  `Microsoft.Extensions.ApiDescription.Server` émet
  `backend/openapi/lorebank.json` à chaque build de l'hôte, le fichier est
  commité et la CI échoue s'il diverge. Le front en génère ses types
  (`openapi-typescript`), jamais commités.
- Le conteneur racine est Autofac (`UseServiceProviderFactory`) ; les
  dépendances s'enregistrent dans des `Module` Autofac, les handlers MediatR
  par scan d'assembly — l'hôte agrège les `ApplicationAssembly` de tous les
  `IHostModule` en un seul `RegisterServicesFromAssemblies`.

## Couche Infrastructure

- EF Core + Npgsql — le package du provider est référencé par
  `LoreBank.SharedKernel.Infrastructure` (ADR 0008), les modules l'héritent en
  transitif. Un `DbContext` par module, un schéma PostgreSQL par module
  (`bank`), migrations dans `Persistence/Migrations` (`dotnet tool run
  dotnet-ef`, manifest dans `backend/.config`). Le démarrage de l'API ne migre
  **jamais** — ni en dev ni ailleurs (ADR 0006) : les migrations s'appliquent
  par `ModuleMigrator` (`LoreBank.SharedKernel.Infrastructure`, jumeau du seam
  `IHostModule`), invoqué par le verbe `migrate` de l'hôte — `dotnet
  LoreBank.Host migrate`, enveloppé par `mise run migrate` — qui compose les
  modules comme l'API puis sort sans servir de HTTP, et par le harnais
  d'intégration pour son Testcontainer. Une migration se génère par la
  commande EF, jamais à la main : skill `nouvelle-migration-schema`.
- Les migrations de **données** s'écrivent en code, jamais en SQL (ADR 0013) :
  une classe `[DataMigration("<timestamp>")]` (14 chiffres, la forme des ids
  EF) dérivant de `DataMigration`, dans `Persistence/DataMigrations/` de
  l'Infrastructure du module (voir `NormalizeLegacyIbans`, l'exemple de
  référence). `ModuleMigrator` fusionne migrations de schéma et de données
  pending en une seule timeline triée par id (`<timestamp>_<NomDeClasse>`) et
  l'applique pas à pas : une migration de données s'intercale entre deux
  migrations de schéma — le triptyque ajouter / backfiller / resserrer tient
  en une release. La logique vit dans le code vivant (les VO d'aujourd'hui,
  jamais leur copie SQL), avec deux corollaires assumés : une migration
  appliquée sur tous les environnements est un artefact mort, supprimable avec
  sa ligne de journal ; et le SQL de bordure reste permis pour les formes
  intermédiaires que le modèle vivant ne matérialise plus.
  `DataMigrationRunner` applique chaque migration dans sa propre transaction,
  ligne de journal incluse (`<schéma>.__data_migrations_history`, créé
  paresseusement) : halte à l'échec sur un état cohérent, reprise au run
  suivant, et le dispatcher d'events de son scope est neutre — une migration
  ne produit aucun fait métier, ses events ont déjà eu lieu. Pas de `Down` —
  revenir en arrière est une restauration de sauvegarde. Chaque migration de
  données a un test qui la rejoue sur des données arrangées en SQL brut — par
  `DataMigrationProbe<TDbContext>`, la surface du harnais : arranger, rejouer,
  relire, et relâcher une contrainte le temps du rejeu, rétablie dans un
  finally, pour le maillon central du triptyque —, et
  `DataMigrationRunnerTest` épingle côté socle le tout-ou-rien et la
  neutralisation des events. Procédure — classe, helpers, timestamp, test de
  rejeu : skill `nouvelle-data-migration`.
- Pas de classes d'entités de persistance : les agrégats du Domain sont mappés
  directement via `IEntityTypeConfiguration` — `HasConversion` pour les VO
  mono-valeur, `OwnsOne` pour les VO multi-champs éclatés en colonnes.
- Seule concession à EF dans le Domain : un constructeur privé sans paramètre
  réservé à la matérialisation. C'est assumé au-delà de la mécanique
  (ADR 0016) : réhydrater un agrégat re-représente un fait établi — aucune
  factory rejouée, aucun invariant revérifié, aucun event réémis — les
  invariants gardent les *transitions*, pas les lectures.
- Le repository implémente le port du Domain en dérivant de
  `ModuleRepository<TAggregate, TId>` (`LoreBank.SharedKernel.Infrastructure`,
  ADR 0010) et ne fournit que sa fabrique de `NotFoundException` : le
  chargement (`FindAsync` — tracker puis clé primaire), le mini-unit-of-work
  (Detached → Add) et le passage par le `SaveChangesAsync` qui dispatche les
  events (ADR 0003) vivent dans la base — un repository écrit sans elle qui
  n'appellerait pas `SaveChangesAsync` casserait le dispatch en silence
  (`ModuleRepositoryTest` épingle ces invariants sur Sqlite). Chaque
  Infrastructure expose son
  `Module` Autofac, enregistré par l'hôte via l'adapter `IHostModule` du module.
- Les readers (`Readers/`) implémentent les ports de lecture de l'Application
  en **LINQ sur des rows keyless** (ADR 0018) : une row par **table** (pas par
  query) — classe plate de primitives dans `Persistence/ReadRows/`, miroir de
  la forme persistée, enregistrée `HasNoKey()` + `ToView("<table>")` dans une
  `IEntityTypeConfiguration` (requêtable, jamais suivie ni écrite, hors
  migrations — le schéma par défaut du module s'applique ; le snapshot porte
  la row, sa création génère une migration vide). Un reader dérive de
  `ModuleReader` (`LoreBank.SharedKernel.Infrastructure`) et compose son
  `Where`/`OrderBy` sur la row via `Query<TRow>()`, puis projette vers son
  Result dans le `Select` final — EF ne lit que les colonnes touchées, aucun
  agrégat n'est matérialisé : `Query` refuse un type à clé ou hors modèle
  (`ModuleReaderTest` épingle ce contrat). Les VO restent les gardiens des
  formats de paramètres (`LedgerAccountRef.ForBankAccount(id).Value` comparé
  au `string` de la row). Une Liste se **déclare** au moteur du socle
  (ADR 0027) : `Query<TRow>().List(query).SearchIn(col).Filter(query.Xxx,
  col, facet: nameof(query.Xxx)).OrderBy(col).ToPageAsync(projection)` —
  le moteur (`ListBuilder`, `SharedKernel.Infrastructure/Readers`) exécute
  le `COUNT`, un `GROUP BY` par facette (comptée hors de son propre filtre :
  le compte dit ce que cocher donnerait), l'`ILIKE` de la recherche (jokers
  échappés) et le `Skip/Take` ; un reader n'écrit jamais ces gestes. Le
  mapping colonne → propriété d'une config keyless
  est en chaînes que rien ne compile, et `ToView` étant hors migrations rien
  ne signale la dérive avec la table : chaque reader doit avoir un test qui
  relit tous ses champs (voir `GetBankAccountByIdTest`). Pas d'échappatoire
  SQL dans les readers — `ModuleSql` est réservé aux migrations de données et
  à l'outbox/inbox.
- Le `DbContext` d'un nouveau module dérive de `ModuleDbContext`
  (`LoreBank.SharedKernel.Infrastructure`) — et c'est tout (voir
  `BankDbContext.cs` : un constructeur, un `DbSet`) : la base dérive le schéma
  de l'identité (le nom du module en minuscules — `bank` ; scellé, renommer un
  schéma = renommer le module — ADR 0009), applique les
  `IEntityTypeConfiguration` de l'assembly du DbContext concret, et porte le
  dispatch des events en interdisant la famille synchrone `SaveChanges` (elle
  perdrait les events en silence). `ConfigureModule` reste comme hook
  optionnel pour une configuration hors `IEntityTypeConfiguration`.
  Rien d'autre à câbler — les handlers sont
  scannés par l'hôte via `DomainAssembly`, la contrainte générique de
  `HostModule<TDbContext>` rend incompilable un `DbContext` monté qui ne
  dériverait pas de la base, et `ModuleCompositionTest` rougit si un handler
  ne se résout pas ou vit hors de la `DomainAssembly`. Inutile d'ignorer la collection d'events dans les
  `IEntityTypeConfiguration` : EF ne mappe pas une propriété sans setter d'un
  type non mappable, et `ModuleDbContextTest` épingle ce contrat.

## Tests

- **Le test avant le code** (ADR 0032) : squelette minimal qui compile →
  test → **RED observé** → corps. Un RED, c'est un test qui compile et
  échoue sur son assertion, jamais une erreur de compilation ; un test écrit
  après coup est taillé sur le code qu'il vient d'écrire et aucune porte ne
  le voit. Un test qui **spécifie** s'écrit avant, un test qui **épingle**
  un contrat (clés JSON, Description) s'écrit après. Les recettes des skills
  portent l'ordre, le récap de l'issue porte la preuve.
- NUnit 4 pour l'exécution, FluentAssertions (pinné en 7.x, dernière version
  sous licence Apache 2.0) pour les assertions : `x.Should().Be(...)`,
  `act.Should().Throw<...>()`. Une classe de test par méthode d'agrégat
  (`Domain/<Agrégat>/<Méthode>Test.cs`), `[TestFixture]` + `[TestOf]`, noms
  `Méthode_ShouldX_WhenY`, sections `// Arrange` / `// Act` / `// Assert` dès
  que le corps a plusieurs phases, omises sur les tests à deux lignes.
- Pas de framework de mock : des fakes manuels (`Fakes/`) pour les ports, des
  builders de test (`Builders/`) pour les agrégats.
- Exception à la règle Domain/Infrastructure : `SharedKernel.Test.Unit` teste
  aussi `SharedKernel.Infrastructure`, parce que le `DomainEventDispatcher` n'a
  aucune E/S et que `ModuleDbContext` se teste sur un Sqlite en mémoire — un
  fake ou une connexion locale suffisent à les isoler, rien ne justifie de les
  remonter jusqu'à `Test.Infrastructure`.
- Le harnais d'intégration vit dans `LoreBank.SharedKernel.Test.Infrastructure`
  (seul projet SharedKernel à référencer l'hôte, et lui-même un vrai projet de
  test) : `IntegrationTestWebAppFactory` démarre l'hôte réel contre un
  Testcontainers PostgreSQL, épingle l'environnement à Development — le
  chargement de la configuration en dépend, et l'`ASPNETCORE_ENVIRONMENT` du
  shell ne doit pas pouvoir en décider (`TestHostEnvironmentTest`) — et redirige
  **toutes** les `ConnectionStrings:*` vers le conteneur — ne rediriger que le DbContext du module courant
  laisserait ceux des autres modules pointer sur la base réelle du développeur,
  que le harnais migrerait via `ModuleMigrator`.
  `ConnectionRedirectTest` épingle cette garantie, et `ModuleCompositionTest`
  itère `HostModules.All` (voir « Architecture »). `TestHost<TFactory>` porte
  l'hôte et le conteneur, partagés par toutes les fixtures d'un assembly, et
  migre le conteneur lui-même — l'API ne le fait plus à son démarrage. Les
  garde-fous du socle (outbox, data migrations) s'ancrent sur le
  **ProbeModule** (`LoreBank.Probe.Infrastructure`, ADR 0017) : le
  module-terrain du harnais — schéma `probe`, adapter `IHostModule` écrit à
  la main — absent de `HostModules.All`, monté par `SharedKernelWebAppFactory`
  seule via le hook `AdditionalModules` de la factory de base, migré par
  `TestHost` avec les autres. Le socle se prouve ainsi sans dépendre des
  modules d'exemple ; `IntegrationEventPublicationTest`, lui, reste ancré sur
  Bank — le module de référence prouve qu'il emprunte le chemin.
- Deux bases dans ce socle : `BaseIntegrationTest<TFactory>` — `TransactionScope`
  rollbacké par test (niveau `ReadCommitted`, celui du `TransactionBehavior` :
  un scope `Required` qui rejoint un ambiant d'un niveau différent lève une
  `ArgumentException`), scope DI, `Sender` — pour les tests qui parlent à
  `ISender` (sa variante `BaseIntegrationTest<TFactory, TDbSetup>` porte en
  plus le `DbSetup` du module, instancié après l'ouverture du scope — l'ordre
  des `[SetUp]` est une garantie du socle, pas un commentaire à recopier ;
  convention : un `DbSetup` a un constructeur `(IServiceProvider)`, celui de
  `DbSetupBase`) ; et `BaseHostTest<TFactory>` — hôte partagé sans transaction —
  pour les dossiers `Apis/` (le `TransactionScope` ambiant ne traverse pas la
  frontière HTTP : `ErrorContractTest`, côté SharedKernel, épingle le contrat
  d'erreur — statuts, type de média, `code`, absence de `detail` — via le
  `ProbeController`, un controller-sonde monté par `SharedKernelWebAppFactory`
  seulement — `DescriptionContractTest` s'y ancre aussi, avec ses actions
  commande / création / lecture / route mixte `[RouteBound]`, jamais
  exécutées, pour épingler que la Description dit ce que `ModuleController`
  fait — plus la sonde de Liste, `GET api/probe/listings`, la seule qui
  s'exécute : `ListContractTest` y prouve la mécanique de l'ADR 0027 sur
  les colonnes `kind`/`active` de `probe_things` —, et
  `ErrorCodesDescriptionTest` que l'enum `ErrorCode` égale le scan
  de `HostModules.All` ; `CqsContractTest`, côté Bank, le fait qu'une commande ne serve
  aucune représentation de bout en bout — la plomberie 204/201+Location est
  celle de `ModuleController`, prouvée en unitaire côté socle
  (`ModuleControllerTest`), le test E2E prouve que le module de référence
  l'emprunte vraiment, Location suivi d'un GET — en
  écrivant pour de vrai, avec des IBAN qui lui sont propres) et pour les tests
  qui observent un rollback réel, comme `TransactionRollbackTest` (un scope
  interne non complété condamne l'ambiant).
- Un module fournit deux petites classes (voir Bank) : une factory scellée
  (`BankWebAppFactory`) qui enregistre ses fakes dans `ConfigureModuleContainer`
  — la substitution d'un service inscrit par un `Module` Autofac ne peut pas se
  faire dans `ConfigureTestServices`, la dernière inscription Autofac gagne et
  le `Module` de l'Infrastructure s'exécute après ; le hook est un second
  `ConfigureContainer` ajouté après celui de l'hôte — et les remet à zéro dans
  `ResetFakes`, appelé par `BaseHostTest` au SetUp et au TearDown (point
  unique, pas de reset à recopier par fixture — la surcharge appelle la base,
  qui efface les fakes du socle : l'horloge, la policy des Signaux et
  l'Acteur, tous trois fournis par le harnais, aucun à réécrire par module) ;
  et un
  `DbSetup : DbSetupBase` (une classe partielle par agrégat), qui crée les
  données via les vrais use cases en **scénario différé** (ADR 0030) :
  chaque geste (`CreateBankAccount`, `Deposit`, `Close`… — un par
  commande ; `RecordDeposit` chez Ledger — un par integration event
  consommé) prend le builder de son entrée (`Builders/`, `<Commande>Builder`
  ou `<Event>Builder` : défauts valides, `With…()`, prérequis nullable posé
  par `Of(id)`), comble le prérequis depuis le dernier créé, empile une
  étape et renvoie le setup ; `RunAsync()` rejoue les étapes dans l'ordre
  (`await DbSetup.CreateBankAccount().Deposit(d => d.WithAmount(50m))
  .RunAsync()`), enveloppe un échec avec le rang et le nom de l'étape, et
  laisse le setup réutilisable ; les accesseurs (`GetLastBankAccountId()`,
  `GetBankAccountAsync()` via le repository) lèvent tant qu'une étape
  attend, et le TearDown rougit sur un scénario jamais joué. Un
  consommateur réutilise les builders du publieur par référence de test à
  test (Ledger → Bank.Test.Infrastructure). Ses fixtures dérivent
  `BaseIntegrationTest<BankWebAppFactory, DbSetup>` directement — pas de
  classe de base par module. Les arranges restent async : bloquer
  (`.Result`) sous le `TransactionScope` ambiant emballerait tout échec en
  `AggregateException` — c'est le différé qui rend le chaînage possible.
  Garde-fou : `DbSetupBaseTest`.
- Les assemblies de test d'intégration déclarent
  `[assembly: Parallelizable(ParallelScope.None)]` : les fakes sont des
  singletons mutables non synchronisés, l'exécution en série est une hypothèse
  déclarée, pas un hasard de configuration. `BaseHostTest` la vérifie au SetUp
  — l'attribut est par assembly et ne s'hérite pas, son oubli casserait au
  premier test avec un message explicite au lieu de flaker en CI.
- Lancer : `mise exec -- dotnet test LoreBank.slnx`.

## Style

- Dès qu'une signature — constructeur (primaire inclus), méthode, opérateur,
  record positionnel — a plus d'un paramètre : retour à la ligne après la
  parenthèse ouvrante, un paramètre par ligne, parenthèse fermante sur sa
  propre ligne. Un seul paramètre reste sur la ligne.
- Même règle pour les invocations (appels, `new`, `throw new`) : dès deux
  arguments, un argument par ligne et **arguments nommés** (`amount: 0m`).
  Un appel à un seul argument reste inline et non nommé.
- Le tout est encodé pour Rider dans `.editorconfig`
  (`resharper_max_formal_parameters_on_line = 1`,
  `resharper_max_invocation_arguments_on_line = 1`,
  `resharper_arguments_* = named`, `resharper_arguments_skip_single = true`)
  — et tenu par Rider et la relecture seulement : Roslyn n'a pas
  d'équivalent, `dotnet format` ne le vérifie pas (ADR 0028). Le reste de
  `.editorconfig` (accolades, `var`, ordre des modificateurs, namespace =
  dossier, encodage, newline finale) est tenu par la porte Format.

## Vérification

Avant de considérer un changement terminé : `mise run check` à la racine —
toutes les portes, dans l'ordre de la CI (build sans warning, format,
Description à jour, suite complète, front typé, linté, formaté, audité ;
`docs/qualite.md`) — et comportement démontré à l'exécution (tests, ou
programme de vérification). Un fichier hors format se corrige par
`mise run format` (`backend/`) ou `mise run format` (`frontend/`), jamais à
la main.

## Agent skills

### Skills du repo

Les procédures de construction vivent dans `.claude/skills/` : huit briques
(value object, agrégat, event handler, commande, query, migration de schéma,
data migration, module) et un chapeau, `ajouter-fonctionnalite`, qui déroule
une issue `ready-for-agent` de bout en bout. Chaque skill nomme ses
garde-fous — les tests du socle qui rougissent si sa règle casse.

### Issue tracker

Les issues vivent dans les GitHub issues du repo, pilotées par le CLI `gh`.
Une issue de spec (label `documentation`) sert de chapeau à une
fonctionnalité et renvoie à ses tickets. Voir
`docs/agents/issue-tracker.md`.

### Triage labels

Labels par défaut, chaque label égal à son nom (`needs-triage`, `needs-info`,
`ready-for-agent`, `ready-for-human`, `wontfix`). Voir
`docs/agents/triage-labels.md`.

### Domain docs

Single-context : un `CONTEXT.md` à la racine + `docs/adr/`. Voir
`docs/agents/domain.md`.
