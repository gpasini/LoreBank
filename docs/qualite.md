# Les portes de qualité, du poste à la CI

Ce que la CI refuse, et le geste qui le rejoue sur le poste — la même tâche
mise dans les deux cas. Décision et alternatives : ADR 0028.

## En une phrase

`mise run check` à la racine rejoue toutes les portes dans l'ordre de la CI ;
chaque step de `.github/workflows/ci.yml` est `mise run <tâche>`, rien de
plus.

## Les portes

| Porte | Rejouer | Corriger |
|---|---|---|
| Build sans warning — compilateur, API bannies, analyseurs CA recommandés, règles IDE élevées, audit NuGet | `mise run //backend:build` | lire l'erreur ; une exemption s'écrit dans `.editorconfig` avec son pourquoi |
| Format .NET — whitespace, encodage, style IDE | `mise run //backend:format:check` | `mise run //backend:format` |
| Description OpenAPI commitée à jour (ADR 0019) | `mise run //backend:openapi:check` | rebuild de l'hôte, commiter `backend/openapi/lorebank.json` |
| Suite complète | `mise run //backend:test` | — |
| Types du front (le Client généré compile) | `mise run //frontend:typecheck` | — |
| Lint et format du front (Biome) | `mise run //frontend:check` | `mise run //frontend:format` |
| Vulnérabilités npm, `high` et plus, dev incluses | `mise run //frontend:audit` | `npm audit fix`, ou une montée de version ciblée |

Depuis `backend/` ou `frontend/`, le préfixe tombe : `mise run format`,
`mise run check`.

## Avant un commit

`mise run pre-commit` rejoue les portes rapides — format .NET en
vérification et `biome check`, jamais la suite d'intégration. Pour qu'il
tourne à chaque commit :

```bash
mise generate git-pre-commit --write
```

C'est un hook local, opt-in : rien n'est versionné dans `.git`, et un
cloneur qui ne le veut pas ne l'installe pas.

## Ce que le build tient déjà

- **Vulnérabilités NuGet** : l'audit du `restore` (NU1901–NU1904) est élevé
  en erreur par `TreatWarningsAsErrors` ; `Directory.Build.props` épingle
  `NuGetAuditMode=all` et `NuGetAuditLevel=low`. Pas de step à part.
- **Les conventions d'architecture** (Domain, Application, composition des
  modules) : `DomainConventionTest`, `ApplicationConventionTest`,
  `ModuleCompositionTest` — un analyseur les dirait moins bien.

## Ce qu'aucune porte ne tient

- La section « Style » de `CLAUDE.md` — un paramètre par ligne, arguments
  nommés — vit en `resharper_*` dans `.editorconfig` : Rider la formate, la
  relecture la tient, Roslyn ne la connaît pas.
- Les licences (les pins restent des commentaires dans
  `Directory.Packages.props`).
- La couverture de code : mesurée et publiée, jamais seuillée — voir
  ci-dessous.

## La couverture : une mesure, pas une porte

`mise run //backend:coverage` rejoue la suite avec le data collector et
rend `backend/coverage/report/` (gitignoré) : `index.html` pour lire ligne
à ligne, `Summary.md` par assembly. En CI, le step Coverage du job backend
écrit ce résumé dans la page du run et publie le HTML en artefact
`coverage-report`. Aucun seuil, hors de `mise run check` : la carte dit où
regarder, elle ne rougit pas (ADR 0029).

La mesure exclut ce qu'aucun test ne vise — le code généré (`obj/`, dont la
source du générateur OpenAPI), les migrations que `ModuleMigrator` rejoue,
le module Probe, les projets de test — dans `backend/coverage.runsettings`.
Rien d'autre : une exclusion de plus déguiserait la carte, et le Gel la
tient.

## Les exemptions

Elles vivent dans `.editorconfig`, chacune avec son pourquoi, à la sévérité
`none` : `CA1711`, `CA1848`, `CA1873`, `CA1859` partout ; `CA1707`,
`CA1001`, `CA1000`, `CA1051`, `CA1710`, `CA1822` dans les projets de test
(la doctrine du harnais). Une nouvelle exemption s'ajoute au même endroit,
règle par règle : une sévérité de catégorie ne bat pas celle
qu'`AnalysisMode` pose sur chaque règle. `IDE0036` (ordre des
modificateurs) et `IDE0130` (namespace = dossier) sont élevées à `warning` ;
`IDE0036` n'est appliquée que par `dotnet format`, pas par le build.

Elles sont **gelées** (ADR 0031). `QualityGateFreezeTest`
(`LoreBank.SharedKernel.Test.Infrastructure/Hosting/`) épingle la liste
exacte des treize lignes `dotnet_diagnostic` avec leur section *et* leur
sévérité — les exemptions comme les deux élévations, qu'un passage à
`suggestion` désarmerait tout aussi silencieusement. En ajouter une est donc
un geste en trois temps : `.editorconfig` avec son pourquoi, la liste gelée
du test, et cette section — le test rougit tant que le code n'est pas nommé
ici.

Le même Gel tient les exclusions de `coverage.runsettings`, les cinq
propriétés de `Directory.Build.props` qui décident de ce que le build
refuse, les symboles de `BannedSymbols.txt` et la liste des Portes
elle-même ; et il interdit ce qui contournerait `.editorconfig` — un test
ignoré, un `SuppressMessage`, un `NoWarn` de projet, un `#pragma warning
disable` hors du code généré par EF, une sévérité de catégorie.

## Pièges

- Une migration générée par EF naît avec un BOM que `.editorconfig` refuse
  (`charset = utf-8`) : `mise run format` après `dotnet-ef migrations add`
  (skill `nouvelle-migration-schema`).
- `dotnet format` compile la solution : compter une à deux minutes à froid.
- `mise run coverage` rejoue toute la suite, instrumentée : en CI, c'est
  une minute de plus après le step Test, jamais à sa place.
- Biome lit `frontend/.gitignore` (`vcs.useIgnoreFile`) : le Client généré
  `src/api/schema.d.ts` n'est ni linté ni formaté.
