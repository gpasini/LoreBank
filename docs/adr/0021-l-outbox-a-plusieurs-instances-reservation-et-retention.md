# L'outbox à plusieurs instances : réservation par bail et rétention

> Statut : accepté — 2026-09-09.

L'ADR 0014 décrit le chemin d'une ligne d'outbox pour un hôte ; il ne disait
rien de ce qui arrive quand plusieurs hôtes le parcourent, et c'est le
déploiement normal d'une API HTTP. Sans rien, deux instances lisaient le
même lot : les deux invoquaient le handler consommateur, le second échouait
sur la clé primaire de l'inbox, et cet échec était compté comme une
tentative — `attempts` montait, `last_error` se polluait, et un effet de
bord non transactionnel du handler partait deux fois. L'inbox rend le rejeu
inoffensif pour la base, pas pour le monde extérieur. Et rien ne supprimait
jamais une ligne livrée : `__outbox` et `__inbox` grossissaient sans fin.
On décide deux choses, portées par le socle. **Réservation** : la lecture
des lignes en attente devient une appropriation en une requête courte
(`UPDATE … SET reserved_until = now() + bail … FOR UPDATE SKIP LOCKED …
RETURNING`), le lot est traité hors verrou, et chaque marquage — livré ou
échec — rend la réservation. Deux instances se partagent les lignes ; une
instance qui disparaît rend les siennes à l'expiration du bail
(`ReservationSeconds`, 5 minutes). **Rétention** : les lignes d'outbox
livrées et les lignes d'inbox traitées plus vieilles que `RetentionDays`
(7 jours) sont purgées par une passe propre du processor, cadencée par le
dispatcher (`PurgeIntervalSeconds`, une heure, première purge dès le
démarrage) ; une seule rétention pour les deux tables, pas d'opt-out. Les
lignes en attente et les lignes poison ne sont jamais purgées.

## Options écartées

- **Verrou consultatif par module au début de la passe**
  (`pg_try_advisory_lock`) : une ligne, libéré tout seul à la déconnexion.
  Écarté parce qu'une seule instance travaille jamais — c'est une élection,
  pas un partage — et que le template se priverait de la mise à l'échelle
  horizontale qu'un hôte HTTP a par ailleurs gratuitement.
- **Instance unique assumée dans l'ADR, sans garde-fou** : l'hypothèse
  serait vraie jusqu'au premier `replicas: 2`, et rien ne rougirait.
- **Réservation ligne à ligne** : bail court, reprise rapide, mais cent
  allers-retours là où le lot en fait un. La reprise en 5 minutes est
  acceptable pour un socle qui ne garantit déjà aucun délai (ADR 0014).
- **Deux rétentions, outbox et inbox** : l'inbox n'a de sens que tant que
  son outbox peut rejouer ; deux durées inviteraient à les désaligner.
- **Un troisième hosted service pour la purge** : l'ADR 0014 tient à un
  seul dépileur ; la purge est une passe de plus du même processor.
- **Un journal de migrations pour les tables du socle** : recréerait
  `ModuleMigrator` pour deux tables. Le DDL du socle reste idempotent et ne
  fait que s'ajouter : `CREATE TABLE IF NOT EXISTS` porte la forme complète
  pour les bases neuves, un `ALTER TABLE … ADD COLUMN IF NOT EXISTS` rattrape
  les anciennes.

## Conséquences

- Une colonne `reserved_until` sur `__outbox`, deux index partiels de purge
  (`dispatched_at` sur l'outbox, `handled_at` sur l'inbox), trois réglages
  de plus dans `OutboxOptions`.
- Un bail qui expire pendant le traitement d'un lot long rend la ligne à
  une autre instance : doublon absorbé par l'inbox côté base, compté en
  tentative. C'est le at-least-once de l'ADR 0014, borné par le bail.
- Bord assumé : une ligne poison qu'un humain réactive après la rétention
  voit ses handlers rejouer, l'inbox ne s'en souvenant plus.
- Un handler consommateur à effet de bord non transactionnel reste exposé
  au rejeu du at-least-once ; la réservation supprime le doublon de
  concurrence, pas celui du crash entre commit et marquage.
