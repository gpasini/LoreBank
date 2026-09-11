# Le Gel : les portes se gardent elles-mêmes

> Statut : accepté — 2026-09-11. Complète l'ADR 0028, dont la ligne « par
> construction » de la table Garde-fous devient un test ; étendu d'un axe par
> l'ADR 0032 (le résidu) et par l'ADR 0035 (la concordance du SDK) ; le front
> gagne son propre Gel, dans son langage, avec l'ADR 0036.

L'ADR 0028 a outillé les Portes, l'ADR 0029 a fait de la Couverture une
mesure. Restait un angle mort : **rien ne relisait les fichiers qui les
définissent**. Un agent coincé sur un avertissement a aujourd'hui un chemin
plus court que corriger le code — trois mots dans `.editorconfig`, une ligne
retirée de `BannedSymbols.txt`, un step supprimé de `ci.yml`. Le build
redevient vert, la CI aussi, et le diff a l'air d'une ligne de
configuration. L'échappatoire d'un agent bloqué est de desserrer la règle,
pas de corriger le code.

Le moment était le bon : l'état était **propre**. Zéro `[Ignore]`, zéro
`Assert.Ignore`, zéro `[Explicit]`, zéro `SuppressMessage`, zéro `NoWarn` de
projet ; les seize `#pragma warning disable` tous dans du code généré par EF
sous `Persistence/Migrations/` ; onze exemptions d'analyseurs commentées une
à une ; quatre exclusions de couverture. On ne gèle pas une dette, on gèle
un état qu'on veut tenir.

## La décision

Un **Gel** : la liste des desserrages admis, épinglée par un test du socle.
Deux régimes, selon que l'axe est vide ou non.

**Interdiction nue** là où l'état est à zéro et doit le rester, parce
qu'aucun franchissement n'a de cas légitime connu :

- `[Ignore]`, `Assert.Ignore`, `[Explicit]` — un test qui ne s'exécute pas
  est une Porte retirée ;
- `SuppressMessage` — une règle supprimée par attribut échappe à
  `.editorconfig` et au recensement ;
- `NoWarn` et `WarningsNotAsErrors` dans un projet ;
- `#pragma warning disable` hors `Persistence/Migrations/` ;
- toute ligne `dotnet_analyzer_diagnostic.*` — une sévérité de catégorie
  désarme une famille entière en une ligne, quand la doctrine est règle par
  règle, avec son pourquoi ;
- `NotImplementedException` dans le code commité — le résidu d'un cycle TDD
  interrompu (ADR 0032) : le squelette écrit pour voir le RED, jamais
  remplacé par son corps. La suite le tient dans le cas courant, où le test
  qu'on vient d'écrire est rouge, mais pas sur un chemin qu'aucun test ne
  traverse.

**Liste gelée** là où l'état est non vide et légitime :

- les **treize** lignes `dotnet_diagnostic.*` de `.editorconfig`, avec leur
  section **et leur sévérité**. Pas seulement les `none` : `IDE0036` et
  `IDE0130` y sont *élevées* à `warning`, et les rabaisser à `suggestion`
  les désarmerait sans qu'un scan des `none` le voie — `IDE0130` est
  précisément la règle que `ModuleCompositionTest` suppose ;
- les **six** valeurs d'exclusion de `coverage.runsettings` ;
- les **cinq** propriétés de `Directory.Build.props` qui décident de ce que
  le build refuse — `TreatWarningsAsErrors`, `AnalysisMode`,
  `EnforceCodeStyleInBuild`, `NuGetAuditMode`, `NuGetAuditLevel`.
  `AnalysisMode` passé à `Default` éteint une trentaine de règles CA sans
  que rien ne le dise ;
- les **quatre** symboles de `BannedSymbols.txt` — les symboles seuls, pas
  les messages : on ne gèle pas de la prose ;
- les entrées de `[tasks.check]` — **huit** à l'origine, dix depuis que le
  front a ses tests et son build (ADR 0036) —, et pour chacune, son step dans
  `.github/workflows/ci.yml`. Une Porte supprimée est le desserrage maximal,
  et c'est ce qui remplace le « par construction » de l'ADR 0028 ;
- la **concordance du SDK** entre `backend/mise.toml` et `backend/global.json`
  (ADR 0035) : deux pins qui divergent, c'est un runner neuf qui installe une
  bande que `global.json` refuse.

Desserrer reste **légitime**. Ce que le Gel refuse, c'est le silence : le
message d'échec le dit, et nomme le fichier à éditer avant le test.

Le Gel **n'est pas une Porte de plus** : c'est un test du socle, dans
`LoreBank.SharedKernel.Test.Infrastructure/Hosting/` avec les autres gardes,
rejoué par `mise run test` et `mise run check`. Aucune tâche mise ni step de
CI nouveaux — un step en shell n'aurait gagné que le cas où la solution ne
compile pas du tout, où rien n'est mergeable de toute façon.

Une dernière assertion empêche la prose de mentir : chacun des codes gelés
doit apparaître dans `docs/qualite.md`. Ajouter une exemption devient un
geste en trois temps — `.editorconfig` avec son pourquoi, la liste gelée, la
doc.

## La mise en conformité

Rien à corriger : la garde a été écrite contre un état déjà propre, et elle
est passée verte du premier coup. Deux gestes seulement.

`TelemetryCompositionTest` remontait déjà au repo depuis `bin/` par un
walker privé pour scanner les `*.csproj` ; il devient `SourceTree`
(`Setups/`), ancré sur le seul marqueur éprouvé — `LoreBank.slnx`, dont
`Root` est le parent. Un seul walker dans le harnais : le second aurait
divergé du premier.

Chaque axe a été prouvé en le desserrant pour de vrai, puis en restaurant :
`[Ignore]` posé, `AnalysisMode` rabaissé, symbole banni retiré, Porte retirée
de `check`, step de CI supprimé, code d'exemption effacé de
`docs/qualite.md`. Tous rougissent.

## Garde-fous

| Règle | Ce qui rougit si elle casse |
|---|---|
| Un test ignoré, un `SuppressMessage`, un `NoWarn` de projet, un `#pragma` hors code généré, une sévérité de catégorie, un `NotImplementedException` commité | `QualityGateFreezeTest` |
| Une exemption d'analyseur ajoutée, retirée, déplacée de section ou changée de sévérité | `QualityGateFreezeTest` |
| Une exclusion de couverture, une propriété de build, un symbole banni modifiés | `QualityGateFreezeTest` |
| Une Porte retirée de `[tasks.check]`, ou sans son step de CI | `QualityGateFreezeTest` |
| Une exemption que `docs/qualite.md` ne nomme pas | `QualityGateFreezeTest` |

Le fichier de la garde porte ses propres motifs en littéraux ; il est scanné
comme les autres, et marque ces lignes-là (`gel:motif`) — elles seules. Une
assertion par réflexion couvre en plus l'assembly du harnais, où un
`[NUnit.Framework.Ignore]` qualifié échapperait au texte.

**Limite assumée** : une fixture désactivée en bloc, ou ce fichier supprimé,
ne peut pas se rattraper elle-même. C'est un diff visible — un fichier de
test qui disparaît, pas trois mots noyés dans 215 lignes de configuration —
et c'est la relecture qui le tient.

## Options écartées

- **Un step de CI en shell** (`grep` sur les fichiers) : il rougirait même
  quand la solution ne compile pas, seul cas qu'un test ne couvre pas — mais
  là rien n'est mergeable de toute façon. Et il ajouterait une tâche mise et
  un step, quand le repo tient « une Porte = une tâche = un step » : le Gel
  n'est pas une Porte.
- **Un fichier de baseline séparé** (`quality-freeze.txt`) que le test lit :
  un format, un parser et un fichier que rien d'autre ne lit. Le fichier du
  test *est* la liste gelée, et son diff *est* le geste explicite.
- **`docs/qualite.md` comme source de vérité parsée** : séduisant — la doc
  ne pourrait plus se périmer — mais il faudrait figer une doc écrite pour
  des humains dans une forme lisible par machine, et son parsing casserait
  au premier reformulage. L'assertion inverse (chaque code gelé *apparaît*
  dans la doc) obtient la même garantie sans contraindre la prose.
- **Étendre cette assertion documentaire à tout le Gel** : `qualite.md`
  devrait alors énumérer les symboles bannis et les tâches, c'est-à-dire
  recopier deux fichiers dans un troisième. `BannedSymbols.txt` porte déjà
  ses pourquoi, et l'ADR 0024 sa doctrine.
- **Geler aussi le front** : `biome.json` n'a aucun override de règles, mais
  deux `// biome-ignore` vivent déjà dans les composants, avec leur raison
  écrite. Une garde .NET qui lirait du `.tsx` installerait une dépendance à
  rebours ; la garde des échappatoires du front vient avec son runner de
  test (issue #15).
- **Geler `[tasks.pre-commit]`** : deux entrées, opt-in, jamais une Porte —
  le geler figerait une commodité.
- **Geler les `resharper_*` de `.editorconfig`** : ils ne sont tenus par
  aucune Porte (ADR 0028 le dit), donc il n'y a pas d'échappatoire à fermer.
