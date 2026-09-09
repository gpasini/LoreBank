# L'identité du module dérivée du DbContext

> Statut : accepté — 2026-09-04 ; prolongé par l'ADR 0008 — la clé de
> connexion rejoint les assemblies dans l'identité dérivée,
> `ConfigureDbContext` gagne un défaut virtuel.

Prolonge l'ADR 0005 : `DbContextType` devient l'ancre de toute l'identité du
module. Le seam `IHostModule` exposait trois `Assembly` remplies par trois
`typeof(X).Assembly` indépendants ; la seule dont l'erreur était silencieuse —
`DomainAssembly` — était aussi la seule sans garde de non-vacuité : mal
pointée, le scan des handlers ne trouve rien, aucun event ne part, et
`ModuleCompositionTest` passe au vert par vacuité. On dérive au lieu de
saisir : la base `HostModule<TDbContext>` (contrainte `ModuleDbContext` — le
test « dérive de ModuleDbContext » devenait incompilable à violer, il est
supprimé) lit `<Racine>.<Module>` sur le nom de l'assembly du DbContext
(parsing épinglé par `ModuleAssemblyNameTest`), charge
`<Racine>.<Module>.{Api,Application,Domain}` dans son constructeur — un projet
mal nommé casse au premier contact avec `HostModules.All` — et expose
`ModuleName`, que `ModuleCompositionTest` relie au 2ᵉ segment du namespace des
`DomainException` du module : le préfixe des codes d'erreur devient correct
par construction, sans re-dériver la conversion. La dérivation ne rouvre pas
l'ADR 0001 : les modules restent déclarés dans une liste explicite, seules
leurs couches sont dérivées d'un module déjà déclaré.

Une garde de composition complète ce que la dérivation ne peut pas voir :
aucun `IDomainEventHandler<>` hors de la `DomainAssembly` — des handlers
rangés dans `Application/` par réflexe Clean Architecture échappaient au scan
avec une `DomainAssembly` pourtant juste, sans que rien ne le signale.

## Options écartées

- **Valider sans dériver** (garder les trois `typeof`, contrôler à la
  construction cohérence et suffixes de couche) : la donnée resterait saisie
  trois fois avec un contrôleur à côté ; dérivée, elle disparaît. Le deletion
  test tranche — supprimer les trois membres concentre la complexité sur un
  fait unique que la doctrine « un module = 6 projets » encode déjà.
- **Racine multi-segment** (`Acme.Fin.Bank.Infrastructure`) : `DomainException`
  lit `segments[1]` du namespace — tolérer une racine composée ferait diverger
  les deux lectures de la même convention (préfixe `FIN.` d'un côté, module
  `Bank` de l'autre). Trois segments exactement, échec bruyant avec un message
  qui nomme la convention et ses deux lecteurs.
- **Dérivation paresseuse** (dans les getters) : trois points d'échec au lieu
  d'un seul ; le constructeur casse une fois, au premier contact avec la
  liste, avant que quoi que ce soit ne soit monté.
