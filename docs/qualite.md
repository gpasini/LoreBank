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
- La couverture de code (issue 20) et les licences (les pins restent des
  commentaires dans `Directory.Packages.props`).

## Les exemptions

Elles vivent dans `.editorconfig`, chacune avec son pourquoi, à la sévérité
`none` : `CA1711`, `CA1848`, `CA1873`, `CA1859` partout ; `CA1707`,
`CA1001`, `CA1000`, `CA1051`, `CA1710`, `CA1822` dans les projets de test
(la doctrine du harnais). Une nouvelle exemption s'ajoute au même endroit,
règle par règle : une sévérité de catégorie ne bat pas celle
qu'`AnalysisMode` pose sur chaque règle. `IDE0036` (ordre des
modificateurs) et `IDE0130` (namespace = dossier) sont élevées à `warning` ;
`IDE0036` n'est appliquée que par `dotnet format`, pas par le build.

## Pièges

- Une migration générée par EF naît avec un BOM que `.editorconfig` refuse
  (`charset = utf-8`) : `mise run format` après `dotnet-ef migrations add`
  (skill `nouvelle-migration-schema`).
- `dotnet format` compile la solution : compter une à deux minutes à froid.
- Biome lit `frontend/.gitignore` (`vcs.useIgnoreFile`) : le Client généré
  `src/api/schema.d.ts` n'est ni linté ni formaté.
