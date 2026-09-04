# Le repository d'agrégat via ModuleRepository

Le geste symétrique de l'ADR 0004, appliqué au troisième pilier de
l'Infrastructure d'un module. Le corps recopié de chaque repository portait
deux invariants sans les nommer : le mini-unit-of-work (Detached → Add, puis
écrire), et surtout le fait que `SaveAsync` passe par le `SaveChangesAsync`
de `ModuleDbContext` — le point exact où les domain events sont dispatchés
(ADR 0003) : un repository maison qui écrirait autrement casserait la chaîne
en silence. Et trois handlers sur quatre recopiaient la paire
`GetByIdAsync(...) ?? throw new XxxNotFoundException(...)`. On absorbe :
`ModuleRepository<TAggregate, TId>` (`SharedKernel.Infrastructure`) porte
`GetRequiredByIdAsync` (non nullable — `FindAsync`, tracker puis clé
primaire, la sémantique d'un chargement d'agrégat — qui lève la
`NotFoundException` du module via une fabrique abstraite) et `SaveAsync`.
Le port du Domain reste écrit par le module — c'est son contrat métier —
mais il se rétrécit : `GetRequiredByIdAsync` **remplace** le `GetByIdAsync`
nullable, dont plus aucun consommateur légitime n'existait (une commande de
création ne charge pas, et une sonde d'existence n'est pas un usage — la
doctrine l'interdit déjà aux queries). `BankAccountRepository` tombe à une
ligne : sa fabrique. La base est testée sur Sqlite comme ses deux jumelles
(`ModuleRepositoryTest`), y compris l'invariant de dispatch.

## Options écartées

- **Garder le `GetByIdAsync` nullable à côté** : une méthode sans consommateur
  dans le module de référence enseigne le doute au cloneur ; le nullable se
  ré-ajoutera si un vrai besoin arrive.
- **Fabrique par paramètre générique** (`TNotFound : NotFoundException` +
  `Activator`) : zéro ligne par repository, mais une convention de
  constructeur vérifiée à l'exécution seulement et une signature à trois
  génériques ; la méthode abstraite est explicite et compile-time.
- **Laisser le `?? throw` dans les handlers** : explicite, mais recopié une
  douzaine de fois pour un module à trois agrégats et quatre commandes — la
  recopie qu'on enterre partout ailleurs.
