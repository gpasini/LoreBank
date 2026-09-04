# Montage de module via IHostModule

> Statut : complété par l'ADR 0005, qui revient sur l'option
> « `Type DbContextType` générique » écartée ci-dessous.

L'hôte doit connaître cinq faits par module (assembly de controllers, assembly
d'application pour MediatR, `Module` Autofac, `DbContext` + chaîne de connexion,
migration en dev) ; inscrits en cinq endroits de `Program.cs`, deux de ces
oublis étaient silencieux. On les regroupe derrière un seam `IHostModule`
(`LoreBank.SharedKernel.Infrastructure`) : un adapter par module dans
`LoreBank.Host/Modules/`, une liste explicite `HostModules.All` comme seule
source de vérité, bouclée par `Program.cs` et itérée par
`ModuleCompositionTest` — un module déclaré est forcément monté et testé.

## Options écartées

- **Scan d'assembly pour découvrir les modules** : liste explicite préférée —
  une ligne par module, ordre déterministe, lisibilité du point d'entrée dans
  un template.
- **`Type DbContextType` générique** (l'hôte ferait `AddDbContext`/`Migrate`
  pour tous) : membres explicites `ConfigureDbContext`/`MigrateAsync` préférés,
  chaque module garde la main sur sa configuration de persistance.
- **7ᵉ projet de composition par module** (référençant Api + Infrastructure,
  l'hôte n'aurait qu'une `ProjectReference`) : un projet d'une classe ne paie
  pas sa place ; l'adapter vit dans l'hôte, la doctrine « un module = 6
  projets » tient.
