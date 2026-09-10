# La couverture : une mesure, pas une porte

> Statut : accepté — 2026-09-09. Remplace le point « couverture » des
> options écartées de l'ADR 0028.

L'ADR 0028 avait retiré `coverlet.collector` — référencé par six projets
de test, jamais collecté — et écarté la couverture, seuillée ou même
collectée : le filet réel du repo est un test par transition d'agrégat,
par reader, par contrat, plus les tests d'architecture, et un pourcentage
n'en dit rien. La question posée à part (issue 20) a été instruite sur des
chiffres, et les chiffres ont changé la réponse à moitié : un seuil n'a
toujours pas de sens, mais la **carte** de ce que les tests n'exécutent pas
en a — mesurée proprement, elle a montré en dix minutes des lignes du socle
que 426 tests ne touchent jamais.

## La décision

La couverture est **collectée et publiée, jamais seuillée**. Elle n'est pas
une Porte au sens de l'ADR 0028 : hors de `mise run check`, elle ne rougit
jamais, et la CI la produit dans un step à part, après Test.

- **Collecte** : `coverlet.collector`, le data collector VSTest — le seul
  des trois outils essayés qui mesure juste dans ce harnais (voir « Options
  écartées ») — déclaré **une fois** dans `Directory.Build.props` sur les
  projets `*.Test.*`. Il ne collecte que quand `mise run coverage` le
  demande ; `mise run test` reste ce qu'il était.
- **Exclusions**, dans `backend/coverage.runsettings` : le code généré
  (`obj/`, dont la source que le générateur OpenAPI de .NET 10 émet dans
  chaque Api — 752 lignes dans `Bank.Api`, exécutées seulement à l'émission
  de la Description), les migrations de schéma (`Persistence/Migrations/**`,
  que `ModuleMigrator` rejoue et que la mesure comptait comme couvertes
  sans test), le module Probe (le terrain du harnais) et les projets de
  test. Rien d'autre : une exclusion par module ou par couche déguiserait
  la carte. Le `Host` reste mesuré.
- **Rendu** : `ReportGenerator` (dotnet tool, manifest `backend/.config`)
  fusionne les six Cobertura en `backend/coverage/report/` (gitignoré) —
  HTML pour lire ligne à ligne, `Summary.md` par assembly. En CI, ce résumé
  va dans la page du run (`GITHUB_STEP_SUMMARY`), le HTML en artefact.

Ce que la carte montre au moment où elle entre : 96,5 % de lignes hors
exclusions, et ses creux — `SharedKernel.Application` 88 % de lignes et
58 % de branches, `Bank.Api` 77,7 %, `Ledger.Domain` 90 %. Ce sont des
endroits à regarder, pas des chiffres à remonter.

## Garde-fous

| Règle | Ce qui rougit si elle casse |
|---|---|
| La carte se produit, sur le poste comme en CI | `mise run //backend:coverage` échoue — tests, collecte ou rendu |
| Elle ne rougit jamais sur un pourcentage | par construction : aucun seuil nulle part, la tâche est hors de `check` |

## Options écartées

- **`dotnet-coverage`** (Microsoft) : ne s'initialise pas sur macOS arm64
  (« Profiler was not initialized »), le poste de référence.
- **`coverlet.console`** (instrumentation hors process) : chiffres faux
  dans ce harnais — `Bank.Application` à 2,5 % et `Bank.Api` à 0 % alors
  que les tests E2E les traversent en HTTP.
- **Un seuil**, global ou par assembly : il mesurerait la proportion de
  migrations et de code généré autant que de règles métier, et une ligne
  de plus dans un `Designer.cs` le ferait bouger. Le filet est ailleurs
  (ADR 0028).
- **La couverture dans `mise run check`** : `check` est la liste de ce qui
  rougit ; une mesure qui ne rougit jamais y brouillerait le contrat.
- **Une déclaration par csproj** de `coverlet.collector` : six lignes qui
  se copient et s'oublient, contre une dans `Directory.Build.props`.
