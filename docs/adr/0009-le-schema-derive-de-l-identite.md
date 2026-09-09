# Le schéma dérivé de l'identité

> Statut : accepté — 2026-09-04.

Prolonge les ADR 0007 et 0008 : après les assemblies et la clé de connexion,
le schéma PostgreSQL rejoint l'identité dérivée. « Un schéma par module » est
une doctrine et une précondition du harnais partagé (la redirection de toutes
les `ConnectionStrings` vers un seul conteneur — ADR 0002 — ne tient que
parce que les modules ne se marchent pas dessus), mais elle reposait sur deux
lignes recopiées dans chaque `ConfigureModule` : `HasDefaultSchema("bank")`,
oubliable et silencieux (tables dans `public`), et
`ApplyConfigurationsFromAssembly(typeof(BankDbContext).Assembly)`, dont le
`typeof` non renommé applique les configurations du mauvais module.
`ModuleDbContext` dérive désormais le schéma dans son constructeur — le nom
du module en minuscules, lu par `ModuleAssemblyName` comme les assemblies —
et son `OnModelCreating` scellé applique lui-même schéma et configurations
(cherchées dans `GetType().Assembly` : plus de `typeof` à renommer) avant un
`ConfigureModule` devenu optionnel. `BankDbContext` tombe à un constructeur
et un `DbSet`. `ModuleReader` expose le schéma aux readers, qui l'interpolent
(`FROM {Schema}.bank_accounts`) au lieu de le réécrire en dur. Deux gardes :
`ModuleDbContextTest` épingle le geste sur Sqlite, et `ModuleCompositionTest`
lit le modèle EF effectif de chaque module — schémas non vides, jamais
`public`, deux à deux distincts. Les fakes de test, dont l'assembly ne suit
pas la convention, passent par un constructeur `internal` à schéma explicite
— un seam interne, la surface publique reste scellée.

Détail assumé : un module multi-mots donnerait `creditcard`, pas
`credit_card` — snake-caser exigerait de dupliquer la regex de frontière de
mots de `DomainException`, une troisième dérivation de la même conversion.

## Options écartées

- **Membre abstrait `Schema`** : un fait unique mais encore saisi, et
  saisissable de travers (`"Bank"`, `"public"`). La dérivation le fait
  disparaître, comme les assemblies en ADR 0007.
- **Dérivé mais virtuel** (surchargeable comme `ConfigureDbContext` en
  ADR 0008) : contrairement à la clé de connexion, aucun scénario légitime ne
  demande un schéma qui diverge du nom du module — la doctrine est « un
  schéma par module », pas « un schéma au choix ». Scellé ; renommer un
  schéma = renommer le module.
- **Garder le littéral du schéma dans le SQL des readers** : ce n'était pas
  un piège silencieux (le test obligatoire par reader casse bruyamment sur un
  schéma faux), mais un fait dérivé qui se réécrit à la main dans chaque
  reader finit par diverger — l'interpolation suit le fait.
