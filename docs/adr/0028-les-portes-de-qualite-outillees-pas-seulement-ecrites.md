# Les portes de qualité : outillées, pas seulement écrites

> Statut : accepté — 2026-09-09.

La doctrine du repo était écrite — « build sans warning », une section
« Style » dans `CLAUDE.md`, un `.editorconfig` de 170 lignes — mais seule sa
première moitié était tenue par une machine : `TreatWarningsAsErrors`, les
API bannies, la Description OpenAPI commitée, la suite complète. Rien ne
vérifiait le format (un fichier mal formaté passait ; 637 écarts dormaient
dans le repo, dont une contradiction interne de `.editorconfig`), aucun
analyseur ne tournait au-delà des API bannies, le front n'avait que son
typecheck, deux vulnérabilités `high` traînaient dans ses dépendances de
dev, et `coverlet.collector` était référencé par six projets sans qu'aucune
couverture ne soit jamais collectée. Un cloneur héritait d'une doctrine à
lire, pas d'une doctrine qui rougit.

## La décision

Une **Porte** est une vérification que la CI tient et qu'un geste `mise`
rejoue à l'identique sur le poste — même tâche, même commande, un step de
CI par tâche. Le socle en fixe six, et ne redit pas ce que le compilateur
ou un test d'architecture tient déjà.

| Porte | Tâche mise | Ce qu'elle tient |
|---|---|---|
| Build | `//backend:build` | `TreatWarningsAsErrors`, API bannies (ADR 0024), analyseurs CA en `AnalysisMode=Recommended`, règles IDE élevées à `warning` par `.editorconfig` (`EnforceCodeStyleInBuild`), audit NuGet du restore |
| Format | `//backend:format:check` | `dotnet format --verify-no-changes` : whitespace, encodage, règles de style IDE et analyseurs, tels que `.editorconfig` les déclare |
| Description | `//backend:openapi:check` | `backend/openapi/lorebank.json` commité égale celui que le build émet (ADR 0019) |
| Suite | `//backend:test` | la suite complète — les tests d'architecture du socle y sont la relecture |
| Front | `//frontend:typecheck`, `//frontend:check` | `tsc` (le front est complet sur les codes d'erreur ou ne compile pas) ; Biome, lint et format en un geste |
| Audit npm | `//frontend:audit` | `npm audit --audit-level=high`, dépendances de dev incluses — elles s'exécutent sur le poste et sur le runner |

`mise run check` à la racine rejoue les six dans l'ordre ; `mise run
pre-commit` ne rejoue que les rapides (format, lint — jamais la suite
d'intégration) et s'installe en hook git par
`mise generate git-pre-commit --write`, opt-in, rien de versionné dans
`.git`.

**Ce que le build tenait déjà et qu'aucune porte ne redit.** L'audit NuGet
de `restore` (NU1901–NU1904) est la porte de vulnérabilités du back :
`TreatWarningsAsErrors` l'élève en erreur. `Directory.Build.props` épingle
`NuGetAuditMode=all` et `NuGetAuditLevel=low` — les défauts du SDK, écrits
pour qu'un changement de défaut ne desserre rien en silence. Les
conventions du Domain, de l'Application et de la composition restent aux
tests d'architecture (`DomainConventionTest`, `ApplicationConventionTest`,
`ModuleCompositionTest`) : un analyseur les dirait moins bien.

**Ce que le socle ne tient pas.** La section « Style » de `CLAUDE.md` — un
paramètre par ligne, arguments nommés — est encodée en `resharper_*` dans
`.editorconfig` : Roslyn n'a pas d'équivalent, seul `jb cleanupcode` la
rejouerait. Elle reste tenue par Rider et par la relecture, et la doc le
dit. Même chose pour `IDE0036` (ordre des modificateurs) : `dotnet format`
le tient, le build ne l'applique pas — c'est la porte Format qui rougit.

**Les exemptions sont écrites à côté de leur pourquoi**, dans
`.editorconfig` : `CA1711` (le suffixe `EventHandler` est le vocabulaire du
socle), `CA1848`/`CA1873` (les délégués `LoggerMessage` sont une
optimisation de chemin chaud, le socle ne journalise que des échecs),
`CA1859` (le socle programme contre des abstractions, en privé aussi), et
pour les projets de test seulement `CA1707`, `CA1001`, `CA1000`, `CA1051`,
`CA1710`, `CA1822` — la doctrine du harnais (noms `Méthode_ShouldX_WhenY`,
libération en `[TearDown]`, hôte partagé en statique d'un générique,
controller-sonde sans état). Règle par règle : une sévérité de catégorie ne
bat pas celle qu'`AnalysisMode` pose sur chaque règle.

## La mise en conformité

Elle a précédé les portes, en commit à part, mécanique : `insert_final_newline`
passé à `true` dans `[*]` (les fichiers l'avaient, la ligne `resharper_*`
le disait, `[*]` disait le contraire), le BOM retiré des migrations
générées par EF (`dotnet format` s'en charge, la skill
`nouvelle-migration-schema` le demande après chaque génération), quinze
fichiers réalignés sur le `csharp_new_line_before_open_brace` qui listait
déjà `lambdas`, et le front reformaté par Biome. Les analyseurs ont ensuite
corrigé de vraies choses : un `CancellationToken` non propagé dans
`TransactionBehavior`, deux `int.ToString()` sensibles à la culture dans la
Description, deux `StartsWith` sans `StringComparison`.

## Garde-fous

| Règle | Ce qui rougit si elle casse |
|---|---|
| Un fichier hors format | `mise run //backend:format:check` (`dotnet format --verify-no-changes`) |
| Une règle CA recommandée ou une règle IDE élevée violée | `mise run //backend:build` — `EnforceCodeStyleInBuild` + `TreatWarningsAsErrors` (`IDE0036` : la porte Format) |
| Un paquet NuGet vulnérable, direct ou transitif | `mise run //backend:build` — NU1901–1904 en erreur au restore |
| Un fichier du front hors format ou une règle Biome violée | `mise run //frontend:check` |
| Une dépendance npm vulnérable en `high` ou plus | `mise run //frontend:audit` |
| Une porte qui existe sur le poste mais pas en CI, ou l'inverse | par construction : chaque step de `.github/workflows/ci.yml` est `mise run <tâche>` |

## Options écartées

- **`jb cleanupcode` en CI** pour tenir aussi les `resharper_*` : une
  dépendance JetBrains imposée à tout cloneur, y compris sous VS Code, et
  une minute de téléchargement par run. Un cloneur sous Rider peut l'ajouter
  en tâche locale.
- **Un analyseur tiers** (Sonar, Roslynator, Meziantou) : les conventions
  qui comptent ici vivent dans les tests d'architecture ; un analyseur
  générique ajouterait des règles à exempter, pas de la doctrine.
- **Une couverture seuillée** : un pourcentage ne dit rien du filet réel
  (un test par transition, par reader, par contrat). Ce point a d'abord
  écarté aussi la collecte et retiré `coverlet.collector` sans usage ;
  l'ADR 0029 le remplace — la couverture est collectée et publiée comme
  une mesure, jamais comme une porte.
- **Une porte de licences** : elle demande un outil et une allowlist, c'est
  la politique du cloneur ; les pins (FluentAssertions 7.x, MediatR 12.x)
  restent des commentaires dans `Directory.Packages.props`.
- **Un hook obligatoire** (lefthook, husky) : une dépendance de plus et un
  geste imposé à chaque commit ; le hook mise est opt-in et sans dépendance.
- **Un `mise run check` unique comme seul step de CI** : un rouge illisible.
  Les steps sont un par tâche ; seule la liste des noms est dupliquée.
- **ESLint + Prettier** au lieu de Biome : le mainstream, mais six paquets
  et deux configurations pour lint et format ; Biome en fait un, cohérent
  avec un front délibérément minimal.
