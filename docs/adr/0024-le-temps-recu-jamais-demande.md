# Le temps reçu, jamais demandé

> Statut : accepté — 2026-09-09.

Aucun agrégat ne portait de date, et le premier module qui en aurait eu
besoin aurait fixé la convention seul — `DateTime.UtcNow` dans une factory,
intestable et invisible. On décide que le Domain **reçoit l'Instant, il ne
le demande pas**, sur le modèle de l'Acteur (ADR 0023) : une transition qui
date un fait prend un `DateTimeOffset` en paramètre, le handler de commande
le demande à `TimeProvider` — l'abstraction de la BCL, enregistrée par
l'hôte en `TimeProvider.System`, remplacée dans le harnais par un fake
manuel du socle — et un handler de domain event reprend l'Instant que le
fait porte, jamais un second. Pas de port du Domain, pas de value object :
un instant n'a pas d'invariant que le provider de persistance ne tienne
déjà (`timestamptz` refuse un offset non nul à l'écriture), et
`GetUtcNow()` rend l'UTC par contrat. La convention est tenue par le
compilateur : un analyseur d'API bannies fait échouer le build de tout
projet `.Domain` ou `.Application` — socle inclus — qui lit `DateTime.Now`,
`DateTime.UtcNow`, `DateTimeOffset.Now` ou `DateTimeOffset.UtcNow`. Le
module de référence montre le geste sur l'ouverture d'un compte
(`OpenedAt`, à côté d'`OpenedBy`), et le Ledger sur la comptabilisation
d'une écriture (`RecordedAt`, l'Instant demandé par le handler
d'integration event : une date de comptabilisation, pas la date du fait
chez Bank).

## Options écartées

- **Un port du Domain `IClock`** implémenté par l'Infrastructure et injecté
  dans les agrégats ou les handlers de domain events : un agrégat qui reçoit
  une valeur se teste sans fake, et un handler de domain event qui redemande
  l'heure fabrique deux dates pour un seul fait.
- **Un port du socle `ICurrentInstant`**, jumeau d'`ICurrentActor` :
  `ICurrentActor` existe parce qu'il rend un VO et cache la lecture du
  principal ; ici la valeur est nue et `TimeProvider` est déjà le seam.
- **Un VO `Instant`** à offset zéro imposé : une conversion partout pour une
  règle que la frontière de persistance tient.
- **Un `OccurredAt` de socle sur `IDomainEvent`** : forcerait chaque
  transition à recevoir un Instant, y compris celles qui n'en veulent pas
  (`Deposit`, `Close`). Un event porte sa date quand le fait en a une ;
  l'outbox garde son `now()` de base, qui est l'instant de publication.
- **Un test de convention** (IL via Mono.Cecil, ou lecture des sources) : la
  doctrine du repo est « build sans warning, tenu par le compilateur », et
  l'analyseur nomme la ligne fautive au moment où on l'écrit.
- **Dater les integration events de Bank** pour que le Ledger enregistre la
  date du fait : triple le périmètre côté Bank et ouvre la question date de
  valeur / date de comptabilisation, qui aura sa propre issue si le Ledger la
  veut un jour.

## Conséquences

- Premier analyseur tiers du template (`Microsoft.CodeAnalysis.BannedApiAnalyzers`,
  Microsoft, MIT), activé par suffixe de nom de projet dans
  `Directory.Build.props`, avec un `BannedSymbols.txt` unique à la racine de
  `backend/`. Sa couverture est un fait de build, pas un test nommé.
- `OutboxDispatcher` (socle Infrastructure) garde son `DateTimeOffset.UtcNow`
  pour la cadence de purge : hors du périmètre de l'analyseur, déjà prouvé
  sans horloge par `PurgeCadenceTest`.
- Les colonnes datées naissent `NOT NULL` par le triptyque de l'ADR 0013 —
  ajout nullable, backfill en code avec l'Instant de la migration,
  resserrage — qui devient l'exemple de référence du triptyque.
- Les mouvements du Ledger sont triés par `RecordedAt` puis identifiant ;
  le tri par identifiant seul (un Guid) était un ordre sans sens.
