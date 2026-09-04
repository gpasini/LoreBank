---
name: nouveau-module
description: À utiliser avant de créer un module métier — un nouveau bounded context avec ses 6 projets, son DbContext et son schéma — ou quand un concept ne rentre dans aucun module existant.
---

# Nouveau module

## Principe

Un module = 6 projets + un adapter d'une ligne côté hôte. Toute l'identité
(nom, assemblies, clé de connexion, schéma PostgreSQL) est **dérivée du
DbContext** par le socle (ADR 0007/0008/0009) : ajouter un module, c'est
écrire son adapter et l'ajouter à `HostModules.All` — rien d'autre côté hôte,
et la batterie `ModuleCompositionTest` le couvre d'office.

## Recette

1. **Les 6 projets** `LoreBank.<Module>.{Domain, Application, Infrastructure,
   Api, Test.Unit, Test.Infrastructure}`, à plat dans `backend/`, ajoutés au
   `LoreBank.slnx` sous `Modules/<Module>`. Références (calquées sur Bank) :
   Domain → SharedKernel.Domain ; Application → Domain +
   SharedKernel.Application ; Infrastructure → Application + Domain +
   SharedKernel.Infrastructure (+ packages `Autofac`,
   `Microsoft.EntityFrameworkCore`, sans attribut `Version` — central package
   management) ; Api → Application + SharedKernel.Api (+
   `FrameworkReference Microsoft.AspNetCore.App`) ; Test.Unit → Domain +
   Application ; Test.Infrastructure → Application +
   SharedKernel.Test.Infrastructure. Le nom fait **exactement trois
   segments** : l'identité en est dérivée.
2. **Le DbContext** : `sealed class <Module>DbContext(options, dispatcher) :
   ModuleDbContext` — un constructeur, des `DbSet` (voir `BankDbContext` : 19
   lignes). Le schéma sera le nom du module en minuscules ; les
   `IEntityTypeConfiguration` de l'assembly sont appliquées d'office.
3. **Le Module Autofac** de l'Infrastructure
   (`<Module>InfrastructureModule : Module`) : repositories, readers,
   implémentations de ports, en `InstancePerLifetimeScope`.
4. **L'adapter** dans `LoreBank.Host/Modules/` :
   `sealed class <Module>Module : HostModule<<Module>DbContext>` qui ne
   déclare que son `AutofacModule` — puis la ligne dans `HostModules.All`.
5. **La chaîne de connexion** `<Module>Db` sous `ConnectionStrings` de
   `appsettings.json` de l'hôte.
6. **Le harnais de test du module** : une factory scellée
   `<Module>WebAppFactory : IntegrationTestWebAppFactory` — fakes dans
   `ConfigureModuleContainer` (pas `ConfigureTestServices` : la dernière
   inscription Autofac gagne, et le `Module` de l'Infrastructure s'exécute
   après), remise à zéro dans `ResetFakes` — et un
   `DbSetup : DbSetupBase` (partial, constructeur `(IServiceProvider)`). Le
   `GlobalUsings.cs` du Test.Infrastructure déclare
   `[assembly: Parallelizable(ParallelScope.None)]`.
7. **Le contenu** vient des autres skills : agrégat et VO (`nouvel-agregat`,
   `nouveau-value-object`), première migration (`nouvelle-migration-schema`),
   use cases (`nouvelle-commande`, `nouvelle-query`) — avec l'
   `ExceptionCodesTest` du module dès la première exception.

## Exemple de référence

Le module `Bank` en entier — en particulier `BankModule` (l'adapter, 5 lignes
utiles), `BankDbContext`, `BankInfrastructureModule`, `BankWebAppFactory` et
`DbSetup`.

## Garde-fous

| Règle | Ce qui rougit si elle casse |
|---|---|
| Module monté : handlers résolus, controllers montés, schéma propre, migrations à jour | `ModuleCompositionTest` — il itère `HostModules.All`, le nouveau module y passe d'office |
| Nom d'assembly `<Racine>.<Module>.<Couche>` | `ModuleAssemblyNameTest`, et échec bruyant au premier contact avec `HostModules.All` |
| DbContext dérivé de `ModuleDbContext` | la contrainte générique de `HostModule<TDbContext>` — incompilable sinon |
| Clé de connexion présente, `Enlist` actif | `AddModuleDbContext` casse à la composition (`ModuleDbContextRegistrationTest`) |
| Le harnais ne touche jamais la base du poste | `ConnectionRedirectTest` — toutes les `ConnectionStrings:*` redirigées vers le Testcontainer |
| Exécution des tests d'intégration en série | `BaseHostTest` vérifie l'attribut au SetUp, message explicite |

## Pièges

- Le schéma est scellé à l'identité : renommer un schéma = renommer le module
  (ADR 0009). Choisir le nom pour durer.
- Un besoin de persistance particulier se règle par `ConfigureDbContext` sur
  l'adapter — en repassant par l'extension du socle, sinon le module reprend à
  sa charge le garde-fou `Enlist`.
- Une commande traverse un seul module : le nouveau module ne rejoint pas une
  transaction d'un autre (règle d'architecture — le `TransactionScope` ambiant
  ne la fait pas respecter).
- Les propriétés MSBuild communes viennent de `Directory.Build.props`, les
  versions de `Directory.Packages.props` : les csproj n'en redéclarent aucune.

## Avant de terminer

Build sans warning et suite complète verte — `ModuleCompositionTest` est le
test de montage du nouveau module, il doit passer sur lui sans aucune ligne de
test écrite ; le premier use case de bout en bout prouve le reste (skills de
l'étape 7).
