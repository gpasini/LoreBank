# Lectures via rows keyless

> Statut : accepté — 2026-09-07 ; remplace l'ADR 0004 — le SQL brut disparaît
> des readers ; `ModuleSql` reste le geste des migrations de données et de
> l'outbox/inbox.

Les readers écrivaient leur SQL à la main : le lien colonne → propriété
n'était vérifié par aucun compilateur (chaque reader exigeait un test de
relecture de tous ses champs) et chaque `SELECT` se rédigeait caractère par
caractère. Désormais une lecture requête une **row keyless** : une classe
plate par **table** (pas par query), miroir de la forme persistée, rangée
dans `Persistence/ReadRows/` de l'Infrastructure et enregistrée
`HasNoKey()` + `ToView("<table>")` — requêtable, jamais suivie, jamais
écrite, hors migrations (la table appartient au modèle d'écriture ; le
snapshot porte la row, d'où une migration vide à sa création). Le reader
compose son `Where`/`OrderBy` sur la row et projette vers son Result dans le
`Select` final : EF ne lit que les colonnes touchées — « le SELECT ne ramène
que les colonnes du DTO » survit tel quel, et le schéma par défaut du module
s'applique au `ToView` sans rien de plus.

`ModuleReader` rétrécit à un seul geste : `Query<TRow>()`, qui refuse un type
à clé ou hors modèle. La règle « une lecture ne matérialise jamais
d'agrégat » cesse d'être une discipline de revue pour devenir une
impossibilité mécanique (`ModuleReaderTest`). Les invariants d'emprunt de
connexion de l'ADR 0004 n'ont plus d'objet côté readers — EF tient sa
connexion — et restent portés par `ModuleSql` pour ses trois canaux.

Le coût est assumé : le mapping d'une config keyless est en chaînes
(`HasColumnName`), et `ToView` étant hors migrations, rien ne signale la
dérive entre une row et sa table. **La doctrine de test est donc conservée
mot pour mot** — chaque reader a un test d'intégration qui relit tous ses
champs, seul filet du mapping. Pas d'échappatoire SQL dans les readers :
aucune lecture existante ne dépasse un `WHERE` + `ORDER BY` ; l'ADR de la
porte s'écrira le jour où un cas intraduisible existera.

## Options écartées

- **CQRS à tables de projection** (la demande d'origine) : aucun JOIN ni vue
  dénormalisée dans le repo — le seul cas de composition est cross-module et
  se résout en mémoire via le port publié. Des read tables persistées
  paieraient le prix du distribué (synchronisation, staleness — incompatible
  avec « une commande ne renvoie rien, le client refait un GET ») sans en
  avoir le problème.
- **LINQ sur le mapping d'écriture** (`Set<BankAccount>().Select(…)`) :
  résout plus complètement le mapping non vérifié (une seule déclaration),
  mais la gymnastique est réelle — owned sans racine de requête
  (`JournalLine` impose un `SelectMany` avec JOIN), FK shadow
  (`journal_entry_id` absent du modèle), et la règle piégeuse « VO entiers
  dans `Where`/`OrderBy`, `.Value` seulement dans le `Select` final »
  (violation = échec runtime). La simplicité d'écriture l'a emporté.
- **Une row par reader** : le nombre de classes et de configs croîtrait avec
  les queries, pas avec les tables.
- **Enregistrer les Results/DTOs des ports directement en keyless** : les
  contraintes de matérialisation EF fuiraient dans les formes wire, et un
  type du langage publié (Contracts) entrerait dans le modèle de persistance
  du module.
- **Garder une échappatoire SQL dans les readers** : dans un template, une
  porte ouverte est une invitation — même logique que la variante liste de
  l'ADR 0004, écrite seulement quand un vrai appelant l'a demandée.
