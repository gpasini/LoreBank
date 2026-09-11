---
name: nouvelle-data-migration
description: À utiliser avant de transformer des données existantes en base — backfill d'une colonne, normalisation de lignes historiques, recodage — ou quand une telle transformation s'apprête à s'écrire en SQL dans une migration de schéma.
---

# Nouvelle data migration

## Principe

Une migration de données s'écrit **en code, jamais en SQL** (ADR 0013) : la
logique vit dans le code vivant — les VO d'aujourd'hui, jamais leur copie
SQL — et la classe devient un artefact mort une fois appliquée partout,
supprimable avec sa ligne de journal. `ModuleMigrator` fusionne migrations de
schéma et de données en une seule timeline triée par id : une data migration
s'intercale entre deux migrations de schéma, le triptyque ajouter /
backfiller / resserrer tient en une release.

## Recette

1. La classe dans `Persistence/DataMigrations/` de l'Infrastructure du
   module : `[DataMigration("<timestamp>")] public sealed class Xxx(XxxDbContext
   context) : DataMigration(context)` — timestamp UTC à 14 chiffres
   (`yyyyMMddHHmmss`, la forme des ids EF), choisi pour placer la migration
   **au bon endroit de la timeline** par rapport aux migrations de schéma
   qu'elle doit suivre ou précéder. **`ExecuteAsync` reste vide** (`throw new
   NotImplementedException()`) : c'est le squelette.
2. **Le test de rejeu**, écrit maintenant, contre ce vide — voir l'étape 5
   pour sa forme complète. **Il doit rougir** : rien n'est transformé. C'est
   le RED de l'ADR 0032, et il vaut ici plus qu'ailleurs — une migration de
   données s'écrit contre un état du stock qu'on ne reverra jamais, et un
   test taillé après coup sur la transformation qu'on vient d'écrire ne dit
   rien du stock réel.
3. `ExecuteAsync` : lire par `QueryAsync` (le SQL de bordure est permis pour
   les formes intermédiaires que le modèle vivant ne matérialise plus —
   `{Schema}` s'interpole via la base), transformer par le code vivant (le VO
   normalise, calcule, valide), réécrire par `ExecuteSqlAsync` — et seulement
   les lignes qui changent.
4. Pas de `Down` : revenir en arrière est une restauration de sauvegarde.
5. La forme du test de rejeu (`Persistence/DataMigrations/XxxTest.cs` du
   Test.Infrastructure du module, sur `BaseHostTest`) : arranger des lignes en
   SQL brut — elles ne peuvent pas passer par les use cases, c'est la raison
   d'être de la migration —, exécuter la migration, relire. Les quatre gestes
   viennent de `DataMigrationProbe<TDbContext>` (`ExecuteAsync`, `ReadAsync`,
   `ReplayAsync`, `WithNullableColumnAsync`) : aucune plomberie ADO à écrire,
   le SQL reçoit le schéma du module. Deux cas au
   minimum : la ligne transformée, et la ligne témoin laissée intacte.
   Nettoyage des lignes arrangées au SetUp **et** au TearDown. Si la migration
   lit une forme intermédiaire que la base migrée jusqu'au bout ne connaît
   plus — le maillon central du triptyque —, envelopper l'arrange et le rejeu
   dans `WithNullableColumnAsync` : la contrainte est relâchée le temps de
   l'action et rétablie dans un finally.
6. Appliquer : `mise run migrate` — chaque migration passe dans sa propre
   transaction, ligne de journal comprise (`<schéma>.__data_migrations_history`) :
   halte à l'échec sur un état cohérent, reprise au run suivant.

## Exemple de référence

`backend/LoreBank.Bank.Infrastructure/Persistence/DataMigrations/20260904060000_NormalizeLegacyIbans.cs`
et son test `NormalizeLegacyIbansTest` — la forme brute relue, le VO `Iban`
qui normalise, la réécriture des seules lignes qui changent.

## Garde-fous

| Règle | Ce qui rougit si elle casse |
|---|---|
| `[DataMigration]` à 14 chiffres, ids uniques | `ModuleCompositionTest` |
| La classe vit dans l'assembly du DbContext — ailleurs, elle échappe au scan | `ModuleCompositionTest` |
| Journalisée après la migration du harnais | `ModuleCompositionTest` |
| Tout-ou-rien par migration, events neutralisés pendant le run | `DataMigrationRunnerTest` (socle) |
| Entrelacement schéma/données par timestamp | `MigrationTimelineTest` (socle) |
| La transformation fait ce qu'elle dit | le test de rejeu — étape 2, son seul garde-fou |

## Pièges

- La règle métier ne se recopie pas en SQL : un `UPDATE … SET iban =
  upper(replace(…))` divergerait du VO le jour où il change — c'est le VO qui
  transforme, le SQL ne fait que lire et écrire.
- Une migration ne produit aucun fait métier : ses events ont déjà eu lieu —
  le dispatcher de son scope est neutre, inutile d'émettre quoi que ce soit.
- Un effet appliqué partout est un artefact mort : la classe et sa ligne de
  journal se suppriment, elles ne s'accumulent pas.
- Le test hérite de `BaseHostTest`, pas de `BaseIntegrationTest` : le runner
  ouvre ses propres transactions, un scope ambiant les ferait dérailler.

## Avant de terminer

Build sans warning, le test de rejeu de l'étape 2 **rouge d'abord, puis**
vert (transformation et témoin), et `ModuleCompositionTest` vert — il prouve le timestamp, le
rangement et la journalisation de bout en bout.
