# Migrations hors du processus API

Le démarrage de l'API ne migre plus — ni en dev ni ailleurs. Migrer au boot
était un trou en prod (réplicas concurrents qui migrent en même temps, verrous
DDL pendant le démarrage, droits DDL donnés au process qui sert du HTTP) et le
confort du F5 en dev masquait cette absence d'histoire de migration. Le
mécanisme — boucler `HostModules.All`, résoudre `DbContextType`,
`MigrateAsync` — devient `ModuleMigrator`, jumeau du seam dans
`SharedKernel.Infrastructure` ; la politique — quand migrer — appartient aux
consommateurs : le verbe `migrate` de l'hôte (`dotnet LoreBank.Host migrate`,
enveloppé par `mise run migrate`), qui compose les modules comme l'API mais
migre et sort sans servir de HTTP, et le harnais d'intégration
(`TestHost<TFactory>`), qui appelle la routine in-process pour préparer son
Testcontainer. Prix assumé : le geste explicite du dev — une API lancée sur
une base en retard casse à la première requête, erreur SQL claire. Aucun test
n'épingle « le démarrage ne migre pas » : c'est une décision d'architecture,
tenue par cet ADR.

## Options écartées

- **Console dédiée `LoreBank.Migrator`** : séparation physique nette, mais un
  projet de plus et une seconde composition (config, DI) à tenir alignée avec
  celle de l'hôte.
- **Bundles EF produits en CI** : aucun code runtime, mais un bundle par
  DbContext et des design-time factories à écrire — casse « ajouter un
  module = son adapter + une ligne dans `HostModules.All` ».
- **Garder l'auto-migration au démarrage en dev** : deux chemins qui font la
  même chose finissent par diverger, et la pureté visée — le process qui sert
  du HTTP ne fait pas de DDL — vaut aussi sur le poste de dev.
