# Les migrations de données dans la timeline du schéma

Une migration de données — le backfill qui accompagne une évolution de schéma —
s'écrit en C#, pas en SQL : une classe `[DataMigration("<timestamp>")]` dérivant
de `DataMigration` (`SharedKernel.Infrastructure`), dans
`Persistence/DataMigrations/` de l'Infrastructure du module, découverte dans
l'assembly du DbContext comme les `IEntityTypeConfiguration`. `ModuleMigrator`
fusionne les ids EF pending et les ids de données pending — même forme
`<timestamp>_<Nom>`, un seul comparateur ordinal — en une timeline unique
appliquée pas à pas : une migration de données s'exécute après la migration de
schéma qu'elle suit et avant celle qui la suit. C'est ce qui rend possible le
triptyque ajouter (schéma) / backfiller (données) / resserrer (schéma) en une
seule release, sans discipline invisible entre deux déploiements.

Le code utilisé est le code vivant d'aujourd'hui : les VO portent la logique
(un backfill d'IBAN appelle `new Iban(...)`, il ne recopie pas la
normalisation en SQL). Deux conséquences assumées. D'abord, une migration
appliquée sur tous les environnements est un artefact **mort, supprimable**
avec sa ligne de journal — l'historique ne se rejoue pas depuis un code
ancien, c'est le prix de ne pas maintenir un second modèle. Ensuite, le modèle
vivant ne sait pas toujours matérialiser l'état intermédiaire où la migration
s'exécute (colonne source déjà sortie du modèle, colonne nouvelle encore
nulle) : le **SQL de bordure** reste permis, via les helpers de la base —
connexion empruntée au DbContext, schéma interpolé, commande enrôlée dans la
transaction. La promesse est « la logique métier jamais en SQL », pas « zéro
SQL ».

L'exécution est portée par `DataMigrationRunner`, un par module : chaque
migration s'applique dans **sa propre transaction, ligne de journal incluse**
(`<schéma>.__data_migrations_history`, créé paresseusement) — jamais appliquée
sans être journalisée ni l'inverse ; un échec arrête la timeline sur un état
cohérent et le run suivant reprend à la migration échouée, le journal faisant
foi. Le scope du runner substitue un `NoOpDomainEventDispatcher` : une
migration re-représente des faits métier déjà établis, leurs events ont déjà
eu lieu — les redispatcher déclencherait les effets de bord une seconde fois,
multipliés par le volume. Pas de `Down` : un backfill n'est pas réversible,
revenir en arrière est une restauration de sauvegarde. Les pending hors
d'ordre (merge de branches) s'appliquent comme chez EF, dans l'ordre des ids —
le filet est l'échec SQL bruyant, pas une règle préventive.
`ModuleCompositionTest` épingle l'attribut bien formé, l'unicité des ids,
l'assembly de rangement et le journal complet ; `DataMigrationRunnerTest` le
tout-ou-rien et la neutralisation des events.

## Options écartées

- **Phase séparée après toutes les migrations de schéma** : plus simple, mais
  interdit le triptyque en une release (le resserrement détruirait la donnée
  pas encore migrée) — la sécurité reposerait sur une discipline que rien ne
  vérifie, à rebours du socle.
- **Un modèle figé par migration** (records embarqués, historique immuable
  rejouable) : c'est réécrire à la main un second modèle à chaque migration,
  et perdre précisément les VO qu'on voulait réutiliser.
- **Dispatcher les events normalement** : cohérent seulement si une migration
  était un replay de gestes métier ; pour un backfill c'est faux et dangereux.
  L'interdiction bruyante (échouer si un event est levé) rendrait le modèle
  vivant inutilisable — les factories émettent toujours leur event de
  naissance.
- **Un `Down` optionnel** : un chemin de retour non testé est un mensonge de
  plus dans le repo, et dé-migrer des données entrelacées avec du schéma est
  un territoire que même EF ne gère pas bien.
- **Squatter `__EFMigrationsHistory`** : une table qui appartient à EF — une
  ligne étrangère peut casser ses outils, et on dépendrait d'un détail
  d'implémentation.
- **Refuser les pending plus anciens que le dernier appliqué** : bloquerait
  aussi les branches parallèles légitimes, pour inventer une règle qu'EF
  lui-même n'a pas.
