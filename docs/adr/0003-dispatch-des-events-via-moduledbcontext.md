# Dispatch des events via ModuleDbContext

Sur les faits de câblage qu'un nouveau module devait recopier, deux étaient à
échec silencieux : la surcharge de `SaveChangesAsync` (ramasse/vide/écrit/
dispatch) et le scan Autofac des `IDomainEventHandler<>` — un oubli, et les
events du module ne sont plus jamais dispatchés, sans rien pour le signaler.
On absorbe les deux derrière le SharedKernel : une base `ModuleDbContext`
(`LoreBank.SharedKernel.Infrastructure`) porte le bloc de save et interdit la
famille synchrone (`NotSupportedException` — un `SaveChanges` sync perdrait
les events en silence) ; `IHostModule` déclare `DomainAssembly`, que l'hôte
scanne en bouclant `HostModules.All`. `ModuleCompositionTest` vérifie les deux :
chaque `DbContext` monté dérive de `ModuleDbContext`, chaque handler d'une
`DomainAssembly` est résolvable.

Découverte en passant : le `builder.Ignore(DomainEvents)` que chaque
`IEntityTypeConfiguration` recopiait était redondant — EF ne mappe pas une
propriété sans setter d'un type non mappable. Les lignes sont supprimées ;
le contrat « jamais mappé » est épinglé par `ModuleDbContextTest`.

## Options écartées

- **`SaveChangesInterceptor` EF plutôt qu'une classe de base** : EF n'a pas de
  hook global « tous les DbContext » — chaque module aurait dû appeler
  `AddInterceptors`, recréant la ligne recopiable et oubliable qu'on supprime.
  La base est vérifiable par composition ; l'interceptor ne l'est pas.
- **Implémenter la famille synchrone en bloquant sur le dispatch** : les
  handlers sont async ; du sync-over-async sous le `TransactionScope` ambiant
  peut deadlocker. Le socle n'a aucun appel sync — le jour où quelqu'un en
  écrit un, il veut un message clair, pas un blocage.
- **Garder le scan des handlers dans le `Module` Autofac de chaque module** :
  c'est la recopie qui rendait l'oubli possible. Même manœuvre qu'ADR 0001 —
  un fait par module, déclaré sur le seam, bouclé par l'hôte, itéré par le
  test. (ADR 0001 règle la découverte des modules, pas le contenu du seam :
  ajouter `DomainAssembly` ne le rouvre pas.)
- **`OnModelCreating` virtuel avec appel à `base` par le module** : une ligne
  oubliable de plus, silencieuse cette fois. La base scelle `OnModelCreating`
  et expose le hook `ConfigureModule` — un module ne peut pas contourner une
  convention future du socle en oubliant d'appeler la base.
