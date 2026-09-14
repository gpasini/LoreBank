# Les portes de qualité, du poste à la CI

Ce que la CI refuse, et le geste qui le rejoue sur le poste — la même tâche
mise dans les deux cas. Décision et alternatives : ADR 0028.

## En une phrase

`mise run check` à la racine rejoue toutes les portes dans l'ordre de la CI ;
chaque step de `.github/workflows/ci.yml` est `mise run <tâche>`, rien de
plus. Une onzième entrée ferme la liste sans être une porte : elle prend
l'empreinte de l'arbre suivi, pour le hook qui rappelle de lancer `check`
(ADR 0033).

## Les portes

| Porte | Rejouer | Corriger |
|---|---|---|
| Build sans warning — compilateur, API bannies, analyseurs CA recommandés, règles IDE élevées, audit NuGet | `mise run //backend:build` | lire l'erreur ; une exemption s'écrit dans `.editorconfig` avec son pourquoi |
| Format .NET — whitespace, encodage, style IDE | `mise run //backend:format:check` | `mise run //backend:format` |
| Description OpenAPI commitée à jour (ADR 0019) | `mise run //backend:openapi:check` | rebuild de l'hôte, commiter `backend/openapi/lorebank.json` |
| Suite complète | `mise run //backend:test` | — |
| Types du front (le Client généré compile) | `mise run //frontend:typecheck` | — |
| Lint et format du front (Biome) | `mise run //frontend:check` | `mise run //frontend:format` |
| Tests du front — socle, écrans, conventions, Gel du front (ADR 0036) | `mise run //frontend:test` | — |
| Build du front — ce que `tsc` accepte et que Vite refuse | `mise run //frontend:build` | — |
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
  `ModuleCompositionTest` — un analyseur les dirait moins bien. Côté front,
  `conventions.test.ts` et `gel.test.ts` (`docs/front.md`, ADR 0036).
- **L'index des ADR** (`docs/adr/README.md`) : `AdrIndexTest` tient la
  carte — une ligne par fichier, titre, statut, un thème par ADR —, la
  numérotation contiguë, le gabarit à partir de 0037, et que toute mention
  `ADR NNNN` du repo désigne un fichier (ADR 0037).

## Ce qu'aucune porte ne tient

Chaque angle mort est **assumé** : écrit ici avec son pourquoi, jamais
espéré (ADR 0035). Une règle qu'on hésite à ajouter à cette liste est une
règle qui vaut un test.

- La section « Style » de `CLAUDE.md` — un paramètre par ligne, arguments
  nommés — vit en `resharper_*` dans `.editorconfig` : Rider la formate, la
  relecture la tient, Roslyn ne la connaît pas.
- Les licences (les pins restent des commentaires dans
  `Directory.Packages.props`).
- La couverture de code : mesurée et publiée, jamais seuillée — voir
  ci-dessous.
- Le **RED observé** avant le code (ADR 0032) : un test écrit après coup est
  vert, et la couverture monte — aucune porte ne distingue un test qui
  spécifie d'un test qui confirme. Le récap de l'issue en porte la trace
  (section « RED observés » de `ajouter-fonctionnalite`), ce qui ne couvre
  que le travail passé par le chapeau. Le Gel tient en revanche le résidu :
  un `NotImplementedException` commité rougit.
- Une `DomainException` **qu'aucun test n'instancie** ne rencontre la garde
  des scalaires de son constructeur qu'en production (ADR 0035). Exiger un
  test par exception serait une garde sur les tests, pas sur le code ; la
  recette de `nouvel-agregat` dicte déjà l'`ExceptionCodesTest`.
- Un `Result` maison nommé **hors de l'heuristique** de
  `DomainConventionTest` (`Reply`, `Answer`…) : la relecture le tient.
- Côté front, **un type de l'API écrit à la main** qui redécrit un Result :
  il compile et diverge en silence, aucun scan ne le distingue d'un type
  d'écran légitime (ADR 0036). La relecture le tient ; `docs/front.md` le
  dit.
- **La relecture elle-même** : rien ne vérifie qu'elle a eu lieu, ni que ses
  écarts ont été traités. La section « Relecture » du récap de l'issue en
  porte la trace, comme « RED observés » porte celle du RED — et ne couvre,
  comme elle, que le travail passé par le chapeau.

Trois règles ont quitté cette liste le 2026-09-11 (ADR 0035) : les erreurs
métier comme exceptions à paramètres scalaires — le constructeur de
`DomainException` et `DomainConventionTest` —, la commande qui ne traverse
pas deux modules — `ApplicationConventionTest` et `ModuleCompositionTest` —,
et le SDK, que `backend/global.json` tenait déjà à la bande près et dont le
Gel tient maintenant la concordance avec `backend/mise.toml`.

## La relecture

Chaque fois que cette page dit « la relecture le tient », c'est de ceci
qu'elle parle : le sous-agent `relecteur` (`.claude/agents/relecteur.md`,
ADR 0038) relit le diff **à contexte frais** — muni de l'issue, de la
section ci-dessus, du bloc Style de `CLAUDE.md`, du vocabulaire de
`CONTEXT.md` et des ADR que le diff touche — et rend les écarts qu'aucun
test ne tient, chacun avec sa source et un verdict : à corriger, à assumer
ici, ou contredit un ADR. Il ne relance aucune porte et ne redouble aucun
test : ce qui est tenu n'est pas son sujet.

Il se lance avant de considérer un changement terminé (`CLAUDE.md`), avec
la base du diff — `HEAD` sur ce repo, la merge-base pour un cloneur en
branche — et le numéro de l'issue. Un écart se corrige, s'assume, se
rouvre, ou se rejette en le disant : le rejet silencieux est le seul vrai
échec. Ce n'est pas un garde-fou au sens du Gel : un jugement ne se rejoue
pas, et rien ne rougit si la relecture est sautée — c'est écrit ci-dessus,
dans la liste de ce qu'aucune porte ne tient.

Le fichier est du Markdown : un cloneur qui travaille sans Claude Code le
donne tel quel, avec le diff, à ce qu'il utilise — ou le lit lui-même comme
une checklist.

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

Le front a la sienne, même doctrine : `mise run //frontend:coverage`
rejoue la suite vitest avec `@vitest/coverage-v8` et rend
`frontend/coverage/` (gitignoré) — `index.html` ligne à ligne, et le
résumé texte dans le terminal. En CI, le step Coverage du job frontend
écrit ce résumé dans la page du run et publie le HTML en artefact
`coverage-report-frontend`. Tout `src/` est mesuré, y compris ce qu'aucun
test ne touche ; sortent le généré (`schema.d.ts`, `vite-env.d.ts`), le
harnais (`src/test/`), les tests, et `main.tsx` qui monte React sur le
DOM réel. Les deux listes et l'absence de seuil sont gelées par
`gel.test.ts` (ADR 0036) : un seuil ferait de la mesure une porte en
silence.

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
`suggestion` désarmerait tout aussi silencieusement. Il tient aussi la
concordance du SDK : `backend/mise.toml` et `backend/global.json` nomment la
même version, et montent d'un même geste (ADR 0035). En ajouter une est donc
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
