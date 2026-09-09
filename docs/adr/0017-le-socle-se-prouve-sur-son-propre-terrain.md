# Le socle se prouve sur son propre terrain — le ProbeModule

> Statut : accepté — 2026-09-06.

Les garde-fous du socle (`OutboxPublisherTest`, `OutboxProcessorTest`,
`DataMigrationRunnerTest` et leurs sondes) étaient ancrés en dur sur le module
d'exemple : `BankDbContext` résolu nommément, discriminants `bank.*`, jusqu'au
DDL de `bank_accounts` recopié dans les sondes de data migration. Or le geste
attendu du template est « clone-le, renomme, supprime les exemples » — et ce
geste cassait précisément la suite qui devait y survivre : le deletion test de
Bank échouait dans le socle.

On donne au harnais son module-terrain, le **ProbeModule** : un seul projet
`LoreBank.Probe.Infrastructure` (la dérivation d'identité passe — module
« Probe », schéma `probe`), un `ProbeDbContext` dérivant `ModuleDbContext`
avec une entité minimale et sa vraie migration EF — le probe emprunte le
chemin complet de `ModuleMigrator`, le socle prouve aussi sa machinerie de
migration sur son propre terrain — et un adapter `IHostModule` écrit à la
main, hors de `HostModule<TDbContext>`, dont les trois assemblies pointent sur
lui-même. C'est la deuxième espèce d'adapter du seam `IHostModule` : la
dérivation complète de l'identité (quatre assemblies chargées eagerly) est une
convention des modules métier, pas une loi du seam. Le ProbeModule est absent
de `HostModules.All` — jamais monté par l'hôte réel : c'est
`SharedKernelWebAppFactory` qui l'enregistre (dans `ConfigureModuleContainer`,
donc après le `ConfigureContainer` de l'hôte — `OutboxPublisher` et
`OutboxProcessor` consomment l'`IEnumerable<IHostModule>` du conteneur), et
`TestHost` le migre via un hook `AdditionalModules` de la factory, vide par
défaut. Les discriminants des sondes deviennent `probe.*` — ils cessent de
mentir sur leur module publieur. Deux frontières tiennent : les sondes de data
migration restent hors de l'assembly du DbContext (la découverte de
`ModuleMigrator` les rejouerait — `ProbeFailingDataMigration` ferait échouer
chaque migration du harnais), et `IntegrationEventPublicationTest` reste
ancré sur Bank — le module de référence prouve qu'il emprunte le chemin, le
socle prouve la mécanique.

## Options écartées

- **L'ancrage sur l'exemple** (statu quo) : décision consciente à l'origine —
  « le module de référence sert de terrain » — mais prise avant que
  « supprimer Bank » ne soit un scénario du template ; le socle vivait aux
  crochets d'un module supprimable.
- **Quatre projets `LoreBank.Probe.{Domain, Application, Api,
  Infrastructure}`** pour réutiliser `HostModule<TDbContext>` tel quel : les
  `Assembly.Load` eagerly du constructeur exigent l'existence physique des
  trois coquilles vides — trois csproj qui ne paient pas leur place.
- **Assouplir le socle** (assemblies tolérées absentes, `InternalsVisibleTo`
  élargi au harnais) : affaiblirait des garanties de production pour un
  besoin de test.
- **Le ProbeModule dans `HostModules.All`** : monté en production, une chaîne
  `ProbeDb` exigée dans chaque environnement (échec à la composition sinon),
  et `ModuleCompositionTest` lui imposerait requests MediatR, controllers et
  couverture de modèle qu'un terrain de sonde n'a pas à avoir.
- **La table-terrain créée en SQL brut par le harnais** : la migration EF
  réelle fait au contraire du chemin de migration lui-même un invariant
  prouvé sur le terrain du socle.
