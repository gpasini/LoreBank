# La Version d'agrégat portée par le socle

> Statut : accepté — 2026-09-09.

Deux commandes concurrentes qui lisent puis modifient le même agrégat
perdaient une écriture en silence : chaque `UPDATE` d'EF ne filtrait que sur
la clé, le second écrasait le premier, les deux clients recevaient un 204 et
le Ledger recevait deux integration events pour un solde qui n'en reflétait
qu'un. `ReadCommitted` ne protège pas un lire-modifier-écrire, et CLAUDE.md
nommait le remède sans que rien ne l'implémente. On le met dans le socle,
pas dans les modules : `ModuleDbContext` pose par convention une propriété
shadow `version` (entier) sur tout type dérivant `AggregateRoot<>`, la
déclare jeton de concurrence, l'incrémente dans son `SaveChangesAsync` quand
la racine ou l'une de ses dépendances owned a changé, et traduit la
`DbUpdateConcurrencyException` d'EF en `ConcurrentUpdateException`
(SharedKernel, code `CONCURRENT_UPDATE`, paramètre `id`) avant le dispatch —
aucun event ne part sur une écriture refusée. `DomainExceptionFilter` la sert
en 409, forme `ApiProblem` comme toute erreur. Un module n'a rien à déclarer
et ne peut pas l'oublier ; `JournalEntry`, append-only, porte une colonne à
zéro et c'est assumé : un opt-out serait un oubli déguisé.

## Options écartées

- **`RepeatableRead` dans `TransactionBehavior`** : sous PostgreSQL, ce
  niveau suffit à ce scénario — l'`UPDATE` de la seconde transaction échoue
  en `40001`. C'est plus court, et c'est pour ça que quelqu'un le proposera.
  Écarté parce que la garantie devient une ligne de configuration qu'on
  éteint sans qu'aucun test rougisse (le risque que l'ADR 0008 ferme pour
  `Enlist=false`), qu'elle protège la transaction et non l'agrégat — l'unité
  de cohérence que le domaine a choisie —, que son comportement dépend du
  moteur (InnoDB ne détecte pas la perte au même niveau, SQL Server la
  transforme en deadlocks) et qu'elle ne se prouve qu'en Testcontainers, là
  où le jeton se prouve sur le Sqlite des tests unitaires du socle. Et le
  bord HTTP resterait entier à écrire : le `40001` est une `PostgresException`
  brute qu'il faudrait toujours traduire, coder et déclarer.
- **`xmin` Npgsql** : rien à mapper ni à incrémenter, mais lié à PostgreSQL
  et invisible sur Sqlite — `ModuleRepositoryTest` ne pourrait pas prouver le
  refus.
- **`Version` publique sur `AggregateRoot<TId>`** : le Domain n'a rien à
  faire d'un compteur d'écritures ; le jour où une lecture voudra l'exposer,
  une row keyless la lira sans toucher au Domain.
- **Traduction dans `ModuleRepository.SaveAsync` ou dans
  `DomainExceptionFilter`** : le repository n'est pas le seul appelant
  possible de `SaveChangesAsync`, et `SharedKernel.Api` ne référence pas EF.
  Le `SaveChangesAsync` du socle est le seul chemin d'écriture et précède le
  dispatch.
- **Rejeu automatique côté Client** : une politique que le cloneur choisit,
  et qui croise l'idempotence HTTP des commandes. Le front affiche le refus.

## Conséquences

- Une colonne `version` sur chaque table d'agrégat, une migration de schéma
  par module (défaut 0, pas de data migration), une clause `WHERE` de plus
  par écriture. Pas de verrou, pas de niveau d'isolation plus strict.
- Le 409 est déclaré uniformément sur toutes les opérations de la
  Description, comme les quatre statuts existants ; `ErrorCode` gagne
  `CONCURRENT_UPDATE` par le scan, et la table de traduction du front cesse
  de compiler tant qu'elle ne le traduit pas.
- Non traité, délibérément : l'écran périmé. L'utilisateur a vu un solde,
  quelqu'un a écrit entre-temps, sa commande recharge l'état courant et
  s'applique dessus — correct, mais surprenant. Régler ce cas demanderait
  d'envoyer la version vue avec la commande (`If-Match`), une décision liée
  à l'idempotence HTTP et laissée au cloneur.
