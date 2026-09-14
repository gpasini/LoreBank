# L'index des ADR : une carte vérifiée, pas une réorganisation

> Statut : accepté — 2026-09-14.

Trente-six ADR, un fichier chacun, nommés en kebab et numérotés dans l'ordre
où les décisions ont été prises. Relu le 2026-09-11 : le corpus est tenu.
Tous portent leur ligne `> Statut`, la supersession est suivie dans les deux
sens avec un vocabulaire stable — *complété par*, *prolongé par*, *remplacé
par*, *amende* —, vingt-sept sur trente citent au moins un voisin, chaque
fichier a ses « Options écartées ».

Ce qui manquait était de la navigation, pas du contenu. Aucun index :
`docs/agents/domain.md` demande à toute skill de « lire les ADR qui touchent
le terrain », et sans carte l'agent ouvre les trente-six (~150 Ko) ou devine
sur le nom du fichier. Un statut invisible : l'ADR 0004 est remplacé depuis
le 2026-09-07 et se lit comme doctrine courante tant qu'on n'ouvre pas sa
deuxième ligne. Un gabarit qui a dérivé sans être décrit : les dix-huit
premiers n'ont que le titre, la prose et « Options écartées » ; « Le coût
assumé » apparaît au 0019, « Conséquences » au 0020, « La décision » et
« Garde-fous » au 0027 — et cette forme tardive est la meilleure, « Garde-
fous » étant la signature du repo, mais rien ne la prescrivait. Et
`docs/adr/` n'était pas listé dans la section Documentation du README.

## La décision

**Une carte, `docs/adr/README.md`, écrite à la main et tenue vraie par un
test du socle.** Deux tables : par numéro — lien, titre, statut, thème — et
par thème. Et une section « Écrire le prochain » qui porte le gabarit.

**Un seul thème par ADR**, celui de sa décision, pas de ses conséquences.
Dix thèmes, dans le vocabulaire de la table « sujet » de CLAUDE.md : Montage
de module, Persistance, Inter-modules, Bord HTTP, Domaine, Harnais de test,
Observabilité, Portes et Gel, Cadrer l'agent, Front. Un thème par ADR fait
de la table par thème une partition, et une partition se vérifie.

**Le statut a deux valeurs** : *accepté*, ou *remplacé par NNNN* quand la
ligne Statut du fichier dit « remplacé par l'ADR NNNN ». Les autres liens ne
sont pas un statut : un ADR complété, prolongé ou amendé reste doctrine
courante. L'ADR 0004 est aujourd'hui le seul remplacé, et c'est exactement
lui que la colonne doit rendre lisible sans ouvrir le fichier.

**Le numéro est un identifiant.** Cinq cent soixante-quatorze mentions
`ADR NNNN` dans deux cent trente-huit fichiers suivis : CLAUDE.md, les
skills, `Program.cs`, `Directory.Build.props`, les csproj, le front. Un ADR
ne se renumérote pas, et ne se supprime pas : il se marque remplacé. Le test
le tient dans les deux sens — les fichiers vont de 0001 à N sans trou, et
toute citation désigne un fichier.

**Le gabarit vit dans l'index**, section « Écrire le prochain », et il est
obligatoire à partir de celui-ci : `# Titre`, `> Statut : accepté —
AAAA-MM-JJ.` en ligne 3, `## La décision`, `## Garde-fous`, `## Options
écartées`. Les trente-six premiers gardent leur forme. Ce fichier est le
premier écrit sur le gabarit, donc le premier que la garde vérifie.

**CLAUDE.md reste le routeur, l'index est la carte.** La table « sujet » de
CLAUDE.md dit où aller pour le geste en cours ; l'index dit tout ce qui a été
décidé, par thème et avec le statut. Une ligne de CLAUDE.md renvoie vers
l'index ; aucune garde de concordance entre les deux — elle forcerait l'un à
devenir la copie de l'autre, et le plafond de CLAUDE.md (ADR 0034) tient déjà
l'autre dérive.

## Garde-fous

`AdrIndexTest` (`LoreBank.SharedKernel.Test.Infrastructure/Hosting/`), dans
la suite complète — pas une Porte de plus : c'est la réponse de l'ADR 0031,
une doctrine tenue par un test du socle.

| Règle | Test qui rougit |
|---|---|
| `docs/adr/` ne porte que des `NNNN-titre.md` et l'index | `Directory_ShouldHoldOnlyAdrsAndTheIndex` |
| Les numéros vont de 0001 à N, sans trou ni doublon | `Files_ShouldBeNumberedContiguously` |
| La ligne 3 de tout ADR est `> Statut : accepté — AAAA-MM-JJ` | `Files_ShouldOpenWithTheStatusLine` |
| À partir de 0037, les trois sections du gabarit sont là | `Files_ShouldFollowTheTemplate` |
| Une ligne d'index par fichier, dans l'ordre, lien = nom du fichier | `Index_ShouldListEveryFileOnce` |
| Le titre de la ligne est le `#` du fichier | `Index_ShouldCarryTheTitleOfEachFile` |
| Le statut de la ligne est dérivé de la ligne Statut | `Index_ShouldCarryTheStatusOfEachFile` |
| La table par thème est une partition, au thème de la ligne | `ThemeTable_ShouldPartitionTheAdrs` |
| Toute mention `ADR NNNN` du repo désigne un fichier | `Citations_ShouldResolveToAFile` |

Le scan des citations traverse tout le repo hors `.git`, `node_modules`,
`bin`, `obj`, `dist` et `coverage` — `SourceTree.RepositoryFiles()`, le
second walker du harnais, à côté de `BackendSources` qui ne voit que le
backend.

Ce qui reste assumé : **le thème est un jugement**. Le test vérifie que
chaque ADR en a un et un seul, pas qu'il est le bon — la relecture le tient,
et un thème mal choisi se corrige en une ligne.

## Options écartées

- **Réorganiser** — sous-dossiers par thème, renumérotation. Le corpus est
  tenu, ce qui manque est une carte. Un journal de décisions est
  chronologique par nature : le grouper par thème combat sa raison d'être,
  alors qu'un index donne le regroupement à coût nul. Et 574 citations
  feraient de la renumérotation un chantier de deux cents fichiers pour rien.
- **Retrofit des premiers ADR au gabarit tardif** — du churn sur des
  décisions stables, pour un bénéfice qui va au lecteur d'archive, pas à
  l'agent. Le gabarit s'impose à partir d'ici.
- **Un index généré**, sur le modèle d'`openapi:check` : chaque ADR déclare
  son thème dans une ligne `> Thème :`, une tâche mise régénère le README,
  une Porte fait `git diff`. C'est le modèle exact de la Description — mais
  la Description ne peut pas s'écrire à la main, et l'index le peut. Le prix
  aurait été les trente-six fichiers touchés, un générateur en shell à la
  racine, et une dixième Porte dans la liste gelée et la CI. Le test dit
  exactement quelle ligne ment, ce qu'un `git diff` ne dit pas.
- **Deux thèmes par ADR.** La table par thème cesserait d'être une
  partition, donc de se vérifier ; et un ADR à cheval se range très bien par
  sa décision. La couche (Domain / Application / Infrastructure / Api) a été
  écartée pour la même raison : 0012, 0016 et 0035 les traversent toutes.
- **Un `TEMPLATE.md` à part** — un fichier de `docs/adr/` de plus que la
  garde devrait exclure, alors que l'agent qui écrit un ADR ouvre déjà
  l'index pour prendre son numéro.
- **Une garde de concordance entre la table de CLAUDE.md et l'index** — voir
  la décision : l'une est un routeur, l'autre une carte, et les forcer à
  concorder ferait de l'une la copie de l'autre.
